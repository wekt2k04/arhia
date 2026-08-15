using System.Threading;
using System.Threading.Tasks;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.Ports;

public interface IWorkflowTemplateRepository
{
    Task<WorkflowTemplate?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowTemplate?> ObtenirDernierApprouveAsync(WorkflowType type, CancellationToken ct = default);
    Task AjouterAsync(WorkflowTemplate template, CancellationToken ct = default);
    Task MettreAJourAsync(WorkflowTemplate template, CancellationToken ct = default);
}
