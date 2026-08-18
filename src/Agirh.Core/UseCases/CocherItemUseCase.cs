using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class CocherItemUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IEmployeeRepository _employees;

    public CocherItemUseCase(IWorkflowInstanceRepository instances, IEmployeeRepository employees)
    {
        _instances = instances;
        _employees = employees;
    }

    public async Task ExecuteAsync(
        UserAccount actor,
        Guid workflowInstanceId,
        Guid itemId,
        ItemEtat etat,
        string? commentaire,
        DateTime dateCoche,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.WorkflowInstanceCocher))
            throw new AccessDeniedException("Seul un RH peut cocher un item de checklist.");

        var instance = await _instances.ObtenirParIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowInstanceId} introuvable.");

        var employee = await _employees.GetByIdAsync(instance.CollaborateurId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {instance.CollaborateurId} introuvable.");

        if (!DepartmentScopeGuard.CanAccessEmployee(actor, employee))
            throw new AccessDeniedException("Un RH ne peut cocher un item que pour un collaborateur de son pôle.");

        instance.Cocher(itemId, etat, actor.Id, dateCoche, commentaire);
        await _instances.MettreAJourAsync(instance, ct);
    }
}
