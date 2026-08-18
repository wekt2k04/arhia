using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class RejeterTemplateUseCase
{
    private readonly IWorkflowTemplateRepository _templates;

    public RejeterTemplateUseCase(IWorkflowTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task ExecuteAsync(UserAccount actor, Guid templateId, string motif, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.TemplateRejeter))
            throw new AccessDeniedException("Seul un compte Admin/Qualité peut rejeter un template.");

        var template = await _templates.ObtenirParIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"Template {templateId} introuvable.");

        template.Rejeter(actor.Id, motif);
        await _templates.MettreAJourAsync(template, ct);
    }
}
