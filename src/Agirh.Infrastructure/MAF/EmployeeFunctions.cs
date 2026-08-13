using System.ComponentModel;
using System.Text.Json.Serialization;
using Agirh.Core.Interfaces;
using Agirh.Domain.Interfaces;

namespace Agirh.Infrastructure.MAF;

public sealed record ConsulterSoldeInput([property: JsonPropertyName("employeeId")] string EmployeeId);

public sealed class ConsulterSoldeTool : MafToolBase<ConsulterSoldeInput>
{
    private readonly IEmployeeRepository _employeeRepo;

    public ConsulterSoldeTool(IEmployeeRepository employeeRepo) => _employeeRepo = employeeRepo;
    public override string Name => "ConsulterSoldeAsync";
    public override string Description => "Consulte le solde de congés et le compte épargne temps d'un collaborateur.";
    public override RoleFlags RequiredRoles => RoleFlags.All;

    protected override async Task<string> ExecuteTypedAsync(ConsulterSoldeInput input, string? requestingUserId = null, CancellationToken ct = default)
    {
        if (!Guid.TryParse(requestingUserId, out var requesterId))
            return "Erreur : impossible de résoudre l'identité de l'appelant. Action refusée.";

        if (!Guid.TryParse(input.EmployeeId, out var targetId))
            targetId = requesterId;

        var requester = await _employeeRepo.GetByIdAsync(requesterId);
        if (requester == null)
            return "Erreur : compte appelant introuvable. Action refusée.";

        if (string.Equals(requester.Role, "Collaborator", StringComparison.OrdinalIgnoreCase) && targetId != requesterId)
            return "Action non autorisée. Vous ne pouvez consulter que votre propre solde.";

        var employee = await _employeeRepo.GetByIdAsync(targetId);
        if (employee == null)
            return "Collaborateur introuvable.";

        return $"Solde de {employee.FirstName} {employee.LastName} :\n" +
               $"- Congés restants : {employee.LeaveBalance} jours\n" +
               $"- Compte Épargne Temps : {employee.CompteEpargneTemps} jours\n" +
               $"- Email : {employee.Email}";
    }
}

public sealed record GenererSoldeToutCompteInput([property: JsonPropertyName("employeeId")] string EmployeeId);

public sealed class GenererSoldeToutCompteTool : MafToolBase<GenererSoldeToutCompteInput>
{
    private readonly IEmployeeRepository _employeeRepo;
    private const decimal IndemniteJournaliereConges = 100m;
    private const decimal IndemniteJournaliereCET = 150m;

    public GenererSoldeToutCompteTool(IEmployeeRepository employeeRepo) => _employeeRepo = employeeRepo;
    public override string Name => "GenererSoldeToutCompteAsync";
    public override string Description => "Génère le solde de tout compte pour un collaborateur sortant. Inclut le calcul des indemnités.";
    public override RoleFlags RequiredRoles => RoleFlags.Admin | RoleFlags.Manager;

    protected override async Task<string> ExecuteTypedAsync(GenererSoldeToutCompteInput input, string? requestingUserId = null, CancellationToken ct = default)
    {
        if (!Guid.TryParse(input.EmployeeId, out var targetId))
        {
            if (!Guid.TryParse(requestingUserId, out targetId))
                return "Erreur : l'ID fourni n'est pas un GUID valide.";
        }

        var employee = await _employeeRepo.GetByIdAsync(targetId);
        if (employee == null)
            return "Collaborateur introuvable.";

        var conges = Math.Max(0, employee.LeaveBalance);
        var cet = Math.Max(0, employee.CompteEpargneTemps);
        var indemniteConges = conges * IndemniteJournaliereConges;
        var indemniteCET = cet * IndemniteJournaliereCET;

        return $"Solde de tout compte pour {employee.FirstName} {employee.LastName} :\n" +
               $"- Congés restants : {conges} jours\n" +
               $"- Indemnité congés : {indemniteConges} €\n" +
               $"- CET : {cet} jours\n" +
               $"- Indemnité CET : {indemniteCET} €\n" +
               $"- Total indemnités : {indemniteConges + indemniteCET} €\n" +
               $"- Date de calcul : {DateTime.UtcNow:dd/MM/yyyy}";
    }
}
