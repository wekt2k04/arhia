using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class ApprouverTemplateUseCase
{
    private readonly IWorkflowTemplateRepository _templates;

    public ApprouverTemplateUseCase(IWorkflowTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task ExecuteAsync(UserAccount actor, Guid templateId, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.TemplateApprouver))
            throw new AccessDeniedException("Seul un compte Admin/Qualité peut approuver un template.");

        var template = await _templates.ObtenirParIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"Template {templateId} introuvable.");

        template.Approuver(actor.Id);
        await _templates.MettreAJourAsync(template, ct);
    }
}
