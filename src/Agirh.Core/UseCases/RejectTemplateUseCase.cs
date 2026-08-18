using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class RejectTemplateUseCase
{
    private readonly IWorkflowTemplateRepository _templates;

    public RejectTemplateUseCase(IWorkflowTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task ExecuteAsync(UserAccount actor, Guid templateId, string reason, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.TemplateReject))
            throw new AccessDeniedException("Seul un compte Admin/Qualité peut rejeter un template.");

        var template = await _templates.GetByIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"Template {templateId} introuvable.");

        template.Reject(actor.Id, reason);
        await _templates.UpdateAsync(template, ct);
    }
}
