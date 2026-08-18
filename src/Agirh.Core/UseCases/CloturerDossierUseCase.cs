using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class CloturerDossierUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IEmployeeRepository _employees;

    public CloturerDossierUseCase(IWorkflowInstanceRepository instances, IEmployeeRepository employees)
    {
        _instances = instances;
        _employees = employees;
    }

    public async Task ExecuteAsync(UserAccount actor, Guid workflowInstanceId, DateTime dateCloture, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.WorkflowInstanceCloturer))
            throw new AccessDeniedException("Seul un RH peut clôturer un dossier.");

        var instance = await _instances.ObtenirParIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowInstanceId} introuvable.");

        var employee = await _employees.GetByIdAsync(instance.CollaborateurId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {instance.CollaborateurId} introuvable.");

        if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
            throw new AccessDeniedException("Un RH ne peut clôturer un dossier que pour un collaborateur de son pôle.");

        instance.Cloturer(dateCloture);
        await _instances.MettreAJourAsync(instance, ct);
    }
}
