using System.Globalization;
using System.Text.Json.Serialization;
using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;

namespace Agirh.Infrastructure.MAF;

public sealed record PoserDemandeCongesInput(
    [property: JsonPropertyName("dateReference")] string? DateReference,
    [property: JsonPropertyName("days")] string? Days,
    [property: JsonPropertyName("employeeId")] string? EmployeeId);

/// <summary>
/// R5 — Outil de création d'une demande de congés (Pending). Corrige le routage
/// historique « LeaveRequest → ConsulterHistoriqueCongesAsync » (lecture seule)
/// qui renvoyait un historique vide au lieu de poser le congé.
/// </summary>
public sealed class PoserDemandeCongesTool : MafToolBase<PoserDemandeCongesInput>
{
    private readonly ILeaveRequestRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public PoserDemandeCongesTool(ILeaveRequestRepository leaveRepo, IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public override string Name => "PoserDemandeCongesAsync";
    public override string Description => "Crée une demande de congés pour un collaborateur (en attente d'approbation).";
    public override RoleFlags RequiredRoles => RoleFlags.All;

    protected override async Task<string> ExecuteTypedAsync(PoserDemandeCongesInput input, string? requestingUserId = null, CancellationToken ct = default)
    {
        if (!Guid.TryParse(requestingUserId, out var requesterId))
            return "Erreur : impossible de résoudre l'identité de l'appelant. Action refusée.";

        if (!Guid.TryParse(input.EmployeeId, out var targetId))
            targetId = requesterId;

        var requester = await _employeeRepo.GetByIdAsync(requesterId);
        if (requester == null)
            return "Erreur : compte appelant introuvable. Action refusée.";

        if (string.Equals(requester.Role, "Collaborator", StringComparison.OrdinalIgnoreCase) && targetId != requesterId)
            return "Action non autorisée. Vous ne pouvez poser un congé que pour vous-même.";

        if (!TryParseDate(input.DateReference, out var startDate))
            return "Erreur : date de début invalide (format attendu JJ/MM/AAAA). Précisez la date de début.";

        if (!int.TryParse(input.Days, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) || days <= 0 || days > 365)
            return "Erreur : nombre de jours invalide. Précisez le nombre de jours de congés.";

        var leave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = targetId,
            StartDate = startDate,
            EndDate = startDate.AddDays(days - 1),
            DaysRequested = days,
            Type = "Conges",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
        };
        await _leaveRepo.AddAsync(leave);
        await _leaveRepo.SaveChangesAsync(ct);

        return $"Votre demande de congés de {days} jour(s) à partir du {startDate:dd/MM/yyyy} a été enregistrée et est en attente d'approbation par votre manager.";
    }

    private static bool TryParseDate(string? value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return DateTime.TryParseExact(value.Trim(), new[] { "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}

public sealed record ConsulterHistoriqueCongesInput([property: JsonPropertyName("employeeId")] string EmployeeId);

public sealed class ConsulterHistoriqueCongesTool : MafToolBase<ConsulterHistoriqueCongesInput>
{
    private readonly ILeaveRequestRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public ConsulterHistoriqueCongesTool(ILeaveRequestRepository leaveRepo, IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public override string Name => "ConsulterHistoriqueCongesAsync";
    public override string Description => "Consulte l'historique des demandes de congés d'un collaborateur.";
    public override RoleFlags RequiredRoles => RoleFlags.All;

    protected override async Task<string> ExecuteTypedAsync(ConsulterHistoriqueCongesInput input, string? requestingUserId = null, CancellationToken ct = default)
    {
        if (!Guid.TryParse(requestingUserId, out var requesterId))
            return "Erreur : impossible de résoudre l'identité de l'appelant. Action refusée.";

        if (!Guid.TryParse(input.EmployeeId, out var targetId))
            targetId = requesterId;

        var requester = await _employeeRepo.GetByIdAsync(requesterId);
        if (requester == null)
            return "Erreur : compte appelant introuvable. Action refusée.";

        if (string.Equals(requester.Role, "Collaborator", StringComparison.OrdinalIgnoreCase) && targetId != requesterId)
            return "Action non autorisée. Vous ne pouvez consulter que votre propre historique de congés.";

        var leaves = await _leaveRepo.GetByEmployeeIdAsync(targetId);
        var leaveList = (leaves ?? Enumerable.Empty<LeaveRequest>()).ToList();

        if (leaveList.Count == 0)
            return "Aucune demande de congés trouvée pour ce collaborateur.";

        var result = $"Historique des congés ({leaveList.Count} demande(s)) :\n";
        foreach (var leave in leaveList.OrderByDescending(l => l.CreatedAt))
        {
            result += $"- Du {leave.StartDate:dd/MM/yyyy} au {leave.EndDate:dd/MM/yyyy} : {leave.DaysRequested}j, {leave.Type} — {leave.Status}\n";
        }
        return result;
    }
}

public sealed record ApprouverDemandeCongesInput(
    [property: JsonPropertyName("leaveRequestId")] string LeaveRequestId,
    [property: JsonPropertyName("approuver")] bool Approuver);

public sealed class ApprouverDemandeCongesTool : MafToolBase<ApprouverDemandeCongesInput>
{
    private readonly ILeaveRequestRepository _leaveRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public ApprouverDemandeCongesTool(ILeaveRequestRepository leaveRepo, IEmployeeRepository employeeRepo)
    {
        _leaveRepo = leaveRepo;
        _employeeRepo = employeeRepo;
    }

    public override string Name => "ApprouverDemandeCongesAsync";
    public override string Description => "Approuve ou refuse une demande de congés en attente.";
    public override RoleFlags RequiredRoles => RoleFlags.Admin | RoleFlags.Manager;

    protected override async Task<string> ExecuteTypedAsync(ApprouverDemandeCongesInput input, string? requestingUserId = null, CancellationToken ct = default)
    {
        if (!Guid.TryParse(input.LeaveRequestId, out var guid))
            return "Erreur : l'ID fourni n'est pas un GUID valide.";

        var leave = await _leaveRepo.GetByIdAsync(guid);
        if (leave == null)
            return "Demande de congés introuvable.";

        if (!Guid.TryParse(requestingUserId, out var requesterId))
            return "Erreur : impossible de résoudre l'identité de l'appelant. Action refusée.";

        var requester = await _employeeRepo.GetByIdAsync(requesterId);
        if (requester == null)
            return "Erreur : compte appelant introuvable. Action refusée.";

        if (string.Equals(requester.Role, "Manager", StringComparison.OrdinalIgnoreCase))
        {
            var owner = await _employeeRepo.GetByIdAsync(leave.EmployeeId);
            if (owner == null
                || owner.Id == requesterId
                || !owner.ManagerId.HasValue
                || owner.ManagerId.Value != requesterId)
                return "Action non autorisée : vous ne pouvez approuver que les demandes de congés des membres de votre équipe.";
        }

        if (leave.Status != "Pending")
            return $"Impossible de modifier cette demande : son statut actuel est '{leave.Status}'.";

        leave.Status = input.Approuver ? "Approved" : "Rejected";
        await _leaveRepo.UpdateAsync(leave);

        return $"Demande de congés {(input.Approuver ? "approuvée" : "refusée")} avec succès.";
    }
}
