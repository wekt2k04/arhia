using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;

namespace Agirh.Core.UseCases;

public sealed class ArchiverDossierUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IEmployeeRepository _employees;

    public ArchiverDossierUseCase(IWorkflowInstanceRepository instances, IEmployeeRepository employees)
    {
        _instances = instances;
        _employees = employees;
    }

    public async Task ExecuteAsync(Domain.Entities.UserAccount actor, Guid workflowInstanceId, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.WorkflowInstanceArchiver))
            throw new AccessDeniedException("Seuls RH et Admin/Qualité peuvent archiver un dossier.");

        var instance = await _instances.ObtenirParIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowInstanceId} introuvable.");

        var employee = await _employees.GetByIdAsync(instance.CollaborateurId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {instance.CollaborateurId} introuvable.");

        if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
            throw new AccessDeniedException("Un RH ne peut archiver un dossier que pour un collaborateur de son pôle.");

        instance.Archiver();
        await _instances.MettreAJourAsync(instance, ct);
    }
}
