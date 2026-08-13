using System.Text.Json;
using Agirh.Core.Interfaces;
using Agirh.Core.Models;
using Agirh.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Agirh.Infrastructure.Services;

public sealed class ZeroTrustDispatcher : IZeroTrustDispatcher
{
    private readonly IEnumerable<IMafTool> _tools;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly ILeaveRequestRepository _leaveRepo;
    private readonly ILogger<ZeroTrustDispatcher> _logger;

    public ZeroTrustDispatcher(
        IEnumerable<IMafTool> tools,
        IEmployeeRepository employeeRepo,
        ILeaveRequestRepository leaveRepo,
        ILogger<ZeroTrustDispatcher> logger)
    {
        _tools = tools;
        _employeeRepo = employeeRepo;
        _leaveRepo = leaveRepo;
        _logger = logger;
    }

    public async Task<DispatchResult> DispatchAsync(DispatchInput input, CancellationToken ct = default)
    {
        var ctx = input.CognitiveContext;
        var identity = input.Identity;

        if (ctx.ConfidenceScore < 0.4f)
        {
            _logger.LogInformation(
                "INTENT_UNRESOLVABLE — confidence {Conf} below 0.4 threshold",
                ctx.ConfidenceScore);
            return new IntentUnresolvable(
                "Pouvez-vous préciser votre demande ? Je n'ai pas bien compris ce que vous souhaitez.");
        }

        if (!identity.IsActive)
        {
            _logger.LogWarning(
                "ACCESS_DENIED_RBAC — account {UserId} is inactive",
                identity.UserId);
            return new DispatchRejected(new AccessDenied
            {
                UserMessage = "Votre compte est inactif. Veuillez contacter un administrateur.",
                RequiredRole = "Active",
                ActualRole = identity.Role,
                DenialCode = DenialCode.ACCOUNT_INACTIVE,
                UserId = identity.UserId,
                Intention = ctx.Intention.ToString(),
                TimestampUtc = DateTime.UtcNow,
            });
        }

        var intentionStr = ctx.Intention.ToString();
        _logger.LogInformation(
            "Intent extracted by Profiler: {Intention} (confidence {Conf})",
            intentionStr, ctx.ConfidenceScore);
        var tool = FindTool(intentionStr);

        if (tool == null)
        {
            _logger.LogInformation(
                "GENERAL_CHAT_FALLBACK — Intention: {Intention}, MainIdea: \"{MainIdea}\", Confidence: {Conf:0.00}, UserId: {UserId}",
                intentionStr, ctx.MainIdea, ctx.ConfidenceScore, identity.UserId);
            return new DispatchToTool(new ToolDispatch
            {
                ToolName = "GeneralChat",
                Parameters = new Dictionary<string, object?>(),
                SourceIntention = ctx.Intention,
            });
        }

        var config = RbacMatrix.Default.GetToolConfig(tool.Name);
        var requiredRoles = config != null
            ? RbacMatrix.ParseAllowedRoles(config.AllowedRoles)
            : tool.RequiredRoles;

        if ((identity.RoleFlag & requiredRoles) == 0)
        {
            _logger.LogWarning(
                "ACCESS_DENIED_RBAC — user {UserId} with role {Role} denied for tool {ToolName} (requires {Required})",
                identity.UserId, identity.Role, tool.Name, requiredRoles);
            return new DispatchRejected(new AccessDenied
            {
                UserMessage = FormatDenialMessage(tool.Name, identity.Role),
                RequiredRole = config != null ? string.Join(", ", config.AllowedRoles) : tool.RequiredRoles.ToString(),
                ActualRole = identity.Role,
                DenialCode = DenialCode.INSUFFICIENT_ROLE,
                UserId = identity.UserId,
                Intention = intentionStr,
                TimestampUtc = DateTime.UtcNow,
            });
        }

        var scopeDenial = await EvaluateScopeAsync(config, ctx, identity, intentionStr);
        if (scopeDenial != null)
        {
            _logger.LogWarning(
                "ACCESS_DENIED_RBAC — SCOPE_MISMATCH: user {UserId} (role {Role}) denied scope for tool {ToolName}",
                identity.UserId, identity.Role, tool.Name);
            return new DispatchRejected(scopeDenial.Value);
        }

        var parameters = ResolveEffectiveParameters(input.ValidatedParameters, ctx, identity);
        _logger.LogInformation(
            "ROUTE_TO_TOOL — {ToolName} for user {UserId} (role {Role}, flags {Flags})",
            tool.Name, identity.UserId, identity.Role, requiredRoles);

        return new DispatchToTool(new ToolDispatch
        {
            ToolName = tool.Name,
            Parameters = parameters,
            SourceIntention = ctx.Intention,
        });
    }

    private IMafTool? FindTool(string intention)
    {
        var toolName = RbacMatrix.Default.ResolveTool(intention);
        if (string.IsNullOrEmpty(toolName))
            return null;

        return _tools.FirstOrDefault(t => t.Name == toolName);
    }

    private async Task<AccessDenied?> EvaluateScopeAsync(
        ToolConfig? config, DynamicContextVector ctx, HardState identity, string intentionStr)
    {
        if (identity.RoleFlag != RoleFlags.Manager)
            return null;

        var employeeId = ctx.ExtractedEntities?.GetValueOrDefault("employee_id");
        if (!string.IsNullOrEmpty(employeeId) && Guid.TryParse(employeeId, out var empGuid))
        {
            var target = await _employeeRepo.GetByIdAsync(empGuid);
            if (target != null)
                return EvaluateOwnerScope(target.Id, target.ManagerId, identity, intentionStr);
        }

        if (config is { RequiresManagerScope: true })
        {
            var leaveRequestId = ctx.ExtractedEntities?.GetValueOrDefault("leave_request_id");
            if (!string.IsNullOrEmpty(leaveRequestId) && Guid.TryParse(leaveRequestId, out var lrGuid))
            {
                var leave = await _leaveRepo.GetByIdAsync(lrGuid);
                if (leave == null)
                    return BuildScopeDenial(identity, intentionStr);

                var owner = await _employeeRepo.GetByIdAsync(leave.EmployeeId);
                if (owner == null)
                    return BuildScopeDenial(identity, intentionStr);

                return EvaluateOwnerScope(owner.Id, owner.ManagerId, identity, intentionStr);
            }
        }

        return null;
    }

    private static AccessDenied? EvaluateOwnerScope(
        Guid ownerId, Guid? ownerManagerId, HardState identity, string intentionStr)
    {
        if (ownerId.ToString() == identity.UserId)
            return null;

        if (ownerManagerId.HasValue && ownerManagerId.Value.ToString() == identity.UserId)
            return null;

        return BuildScopeDenial(identity, intentionStr);
    }

    private static AccessDenied BuildScopeDenial(HardState identity, string intentionStr) => new()
    {
        UserMessage = "Vous ne pouvez accéder aux données que des membres de votre équipe.",
        RequiredRole = "Manager (scope équipe)",
        ActualRole = identity.Role,
        DenialCode = DenialCode.SCOPE_MISMATCH,
        UserId = identity.UserId,
        Intention = intentionStr,
        TimestampUtc = DateTime.UtcNow,
    };

    private static Dictionary<string, object?> ResolveEffectiveParameters(
        JsonElement? validatedParameters, DynamicContextVector ctx, HardState identity)
    {
        if (validatedParameters is { ValueKind: JsonValueKind.Object } validated)
        {
            var parameters = JsonSerializer.Deserialize<Dictionary<string, object?>>(validated.GetRawText())
                ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            // Le PreFlight n'a rien produit (ex. GenererSoldeToutCompteAsync, EnvoyerAlerteManagerAsync,
            // GeneralChat) : on replie sur la résolution classique (employee_id / query, etc.).
            if (parameters.Count > 0)
                return parameters;
        }

        return ResolveParameters(ctx, identity);
    }

    private static Dictionary<string, object?> ResolveParameters(
        DynamicContextVector ctx, HardState identity)
    {
        var p = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (ctx.ExtractedEntities?.TryGetValue("employee_id", out var empId) == true && !string.IsNullOrEmpty(empId))
            p["employeeId"] = empId;

        if (ctx.ExtractedEntities?.TryGetValue("leave_request_id", out var lrId) == true && !string.IsNullOrEmpty(lrId))
            p["leaveRequestId"] = lrId;

        if (ctx.ExtractedEntities?.TryGetValue("category", out var cat) == true && !string.IsNullOrEmpty(cat))
            p["category"] = cat;

        p["query"] = ctx.MainIdea;

        return p;
    }

    private static string FormatDenialMessage(string toolName, string actualRole)
    {
        return toolName switch
        {
            "RevoquerAccesITAsync" => "Seuls les administrateurs peuvent révoquer des accès IT.",
            "GenererSoldeToutCompteAsync" => "Seuls les administrateurs et managers peuvent générer un solde de tout compte.",
            "ApprouverDemandeCongesAsync" => "Seuls les administrateurs et managers peuvent approuver des demandes de congés.",
            "GenererChecklistAsync" => "Seuls les administrateurs et managers peuvent consulter les checklists.",
            "EnvoyerAlerteManagerAsync" => "Seuls les administrateurs et managers peuvent envoyer des alertes.",
            _ => $"Action non autorisée pour votre rôle ({actualRole}).",
        };
    }
}
