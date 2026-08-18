using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class ProposeTemplateUseCase
{
    private readonly IWorkflowTemplateRepository _templates;

    public ProposeTemplateUseCase(IWorkflowTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task<WorkflowTemplate> ExecuteAsync(
        UserAccount actor,
        WorkflowType type,
        string version,
        IReadOnlyCollection<TemplateSection> sections,
        DateTime createdAt,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.TemplatePropose))
            throw new AccessDeniedException("Seul un RH peut proposer un template (rôle de Rédacteur).");

        var template = new WorkflowTemplate(Guid.NewGuid(), type, version, actor.Id, sections, createdAt);
        template.Submit();

        await _templates.AddAsync(template, ct);
        return template;
    }
}
