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

    public async Task<WorkflowTemplate> ExecuterAsync(
        CompteUtilisateur acteur,
        WorkflowType type,
        string version,
        IReadOnlyCollection<TemplateSection> sections,
        DateTime dateCreation,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.EstAutorise(acteur.Role, ResourceAction.TemplateProposer))
            throw new AccesRefuseException("Seul un RH peut proposer un template (rôle de Rédacteur).");

        var template = new WorkflowTemplate(Guid.NewGuid(), type, version, acteur.Id, sections, dateCreation);
        template.Soumettre();

        await _templates.AjouterAsync(template, ct);
        return template;
    }
}
