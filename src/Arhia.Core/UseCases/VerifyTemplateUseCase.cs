using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;

namespace Arhia.Core.UseCases;

public sealed class VerifyTemplateUseCase
{
    private readonly IWorkflowTemplateRepository _templates;

    public VerifyTemplateUseCase(IWorkflowTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task ExecuteAsync(Domain.Entities.UserAccount actor, Guid templateId, CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.TemplateVerify))
            throw new AccessDeniedException("Seul un compte Admin/Qualité peut vérifier un template.");

        var template = await _templates.GetByIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"Template {templateId} introuvable.");

        template.Verify(actor.Id);
        await _templates.UpdateAsync(template, ct);
    }
}
