using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Domain.Entities;

namespace Arhia.Core.UseCases;

public sealed class ApproveTemplateUseCase
{
    private readonly IWorkflowTemplateRepository _templates;

    public ApproveTemplateUseCase(IWorkflowTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task ExecuteAsync(UserAccount actor, Guid templateId, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.TemplateApprove))
            throw new AccessDeniedException("Seul un compte Admin/Qualité peut approuver un template.");

        var template = await _templates.GetByIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"Template {templateId} introuvable.");

        template.Approve(actor.Id);
        await _templates.UpdateAsync(template, ct);
    }
}
