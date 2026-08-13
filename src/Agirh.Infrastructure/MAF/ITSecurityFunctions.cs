using System.ComponentModel;
using System.Text.Json.Serialization;
using Agirh.Core.Interfaces;
using Agirh.Domain.Interfaces;

namespace Agirh.Infrastructure.MAF;

public sealed record RevoquerAccesITInput([property: JsonPropertyName("employeeId")] string EmployeeId);

public sealed class RevoquerAccesITTool : MafToolBase<RevoquerAccesITInput>
{
    private readonly IEmployeeRepository _employeeRepo;

    public RevoquerAccesITTool(IEmployeeRepository employeeRepo) => _employeeRepo = employeeRepo;
    public override string Name => "RevoquerAccesITAsync";
    public override string Description => "Révoque les accès IT d'un collaborateur. Action définitive.";
    public override RoleFlags RequiredRoles => RoleFlags.Admin;

    protected override async Task<string> ExecuteTypedAsync(RevoquerAccesITInput input, string? requestingUserId = null, CancellationToken ct = default)
    {
        if (!Guid.TryParse(input.EmployeeId, out var guid))
            return "Erreur : l'ID fourni n'est pas un GUID valide.";

        var employee = await _employeeRepo.GetByIdAsync(guid);
        if (employee == null)
            return "Collaborateur introuvable.";

        if (!employee.IsActive)
            return $"Les accès de {employee.FirstName} {employee.LastName} ont déjà été révoqués.";

        employee.IsActive = false;
        await _employeeRepo.UpdateAsync(employee);

        return $"Accès IT révoqués avec succès pour {employee.FirstName} {employee.LastName} ({employee.Email}).";
    }
}

public sealed record EnvoyerAlerteManagerInput(
    [property: JsonPropertyName("employeeId")] string EmployeeId,
    [property: JsonPropertyName("message")] string Message);

public sealed class EnvoyerAlerteManagerTool : MafToolBase<EnvoyerAlerteManagerInput>
{
    private readonly IEmployeeRepository _employeeRepo;

    public EnvoyerAlerteManagerTool(IEmployeeRepository employeeRepo) => _employeeRepo = employeeRepo;
    public override string Name => "EnvoyerAlerteManagerAsync";
    public override string Description => "Envoie une alerte au manager d'un collaborateur concernant un problème ou une situation nécessitant son attention.";
    public override RoleFlags RequiredRoles => RoleFlags.Admin | RoleFlags.Manager;

    protected override async Task<string> ExecuteTypedAsync(EnvoyerAlerteManagerInput input, string? requestingUserId = null, CancellationToken ct = default)
    {
        if (!Guid.TryParse(input.EmployeeId, out var guid))
            return "Erreur : l'ID fourni n'est pas un GUID valide.";

        var message = string.IsNullOrWhiteSpace(input.Message) ? "Alerte RH" : input.Message;

        var employee = await _employeeRepo.GetByIdAsync(guid);
        if (employee == null)
            return "Collaborateur introuvable.";

        if (employee.ManagerId == null)
            return $"Aucun manager assigné à {employee.FirstName} {employee.LastName}.";

        var manager = await _employeeRepo.GetByIdAsync(employee.ManagerId.Value);
        if (manager == null)
            return "Manager introuvable.";

        return $"Alerte envoyée avec succès à {manager.FirstName} {manager.LastName} ({manager.Email}) concernant {employee.FirstName} {employee.LastName}.\nMessage : {message}";
    }
}
