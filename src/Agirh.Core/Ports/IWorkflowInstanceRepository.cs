using System.Threading;
using System.Threading.Tasks;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.Ports;

public interface IWorkflowInstanceRepository
{
    Task<WorkflowInstance?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowInstance?> ObtenirParCollaborateurAsync(Guid collaborateurId, WorkflowType type, CancellationToken ct = default);
    Task AjouterAsync(WorkflowInstance instance, CancellationToken ct = default);
    Task MettreAJourAsync(WorkflowInstance instance, CancellationToken ct = default);
}
