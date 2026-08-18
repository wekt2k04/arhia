using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class ProposerTemplateUseCase
{
    private readonly IWorkflowTemplateRepository _templates;

    public ProposerTemplateUseCase(IWorkflowTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task<WorkflowTemplate> ExecuteAsync(
        UserAccount actor,
        WorkflowType type,
        string version,
        IReadOnlyCollection<TemplateSection> sections,
        DateTime dateCreation,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.TemplateProposer))
            throw new AccessDeniedException("Seul un RH peut proposer un template (rôle de Rédacteur).");

        var template = new WorkflowTemplate(Guid.NewGuid(), type, version, actor.Id, sections, dateCreation);
        template.Soumettre();

        await _templates.AjouterAsync(template, ct);
        return template;
    }
}
