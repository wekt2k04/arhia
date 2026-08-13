using Agirh.Core.Interfaces;

namespace Agirh.Core.Models;

public sealed record ToolConfig(string ToolName, string[] AllowedRoles, bool RequiresManagerScope);

public sealed class RbacMatrix
{
    public IReadOnlyList<ToolConfig> Tools { get; }
    public IReadOnlyDictionary<string, string> IntentToToolMapping { get; }

    private RbacMatrix(IReadOnlyList<ToolConfig> tools, IReadOnlyDictionary<string, string> intentToToolMapping)
    {
        Tools = tools;
        IntentToToolMapping = intentToToolMapping;
    }

    public static RbacMatrix Default { get; } = new(
    [
        new("ConsulterSoldeAsync",               ["Collaborator", "Manager", "Admin"], false),
        new("GenererSoldeToutCompteAsync",       ["Admin", "Manager"],                 false),
        new("RevoquerAccesITAsync",              ["Admin"],                            false),
        new("ApprouverDemandeCongesAsync",       ["Admin", "Manager"],                 true),
        new("EnvoyerAlerteManagerAsync",         ["Admin", "Manager"],                 false),
        new("ConsulterHistoriqueCongesAsync",    ["Collaborator", "Manager", "Admin"], true),
        new("PoserDemandeCongesAsync",           ["Collaborator", "Manager", "Admin"], false),
        new("GenererChecklistAsync",             ["Admin", "Manager"],                 false),
        new("RechercherInformationRagAsync",     ["Collaborator", "Manager", "Admin"], false),
        new("DemanderAvanceSalaireAsync", ["Collaborator", "Manager", "Admin"], false),
    ],
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["LeaveBalance"]        = "ConsulterSoldeAsync",
        ["LeaveRequest"]        = "PoserDemandeCongesAsync",
        ["PayrollSettlement"]   = "GenererSoldeToutCompteAsync",
        ["OnboardingChecklist"] = "GenererChecklistAsync",
        ["KnowledgeSearch"]     = "RechercherInformationRagAsync",
        ["ITAccessRevocation"]  = "RevoquerAccesITAsync",
        ["ManagerAlert"]        = "EnvoyerAlerteManagerAsync",
        ["LeaveApproval"]       = "ApprouverDemandeCongesAsync",
        ["SalaryAdvance"]       = "DemanderAvanceSalaireAsync",
    });

    public string? ResolveTool(string intention)
    {
        IntentToToolMapping.TryGetValue(intention, out var tool);
        return tool;
    }

    public ToolConfig? GetToolConfig(string toolName)
    {
        return Tools.FirstOrDefault(t =>
            t.ToolName.Equals(toolName, StringComparison.OrdinalIgnoreCase));
    }

    public static RoleFlags ParseAllowedRoles(IEnumerable<string> roles)
    {
        var flags = RoleFlags.None;
        foreach (var role in roles)
        {
            flags |= role switch
            {
                "Admin" => RoleFlags.Admin,
                "Manager" => RoleFlags.Manager,
                "Collaborator" => RoleFlags.Collaborator,
                _ => RoleFlags.None
            };
        }
        return flags;
    }
}
