using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;

namespace Agirh.Core.UseCases;

public sealed class VerifierTemplateUseCase
{
    private readonly IWorkflowTemplateRepository _templates;

    public VerifierTemplateUseCase(IWorkflowTemplateRepository templates)
    {
        _templates = templates;
    }

    public async Task ExecuterAsync(Domain.Entities.CompteUtilisateur acteur, Guid templateId, CancellationToken ct = default)
    {
        if (!RbacMatrix.EstAutorise(acteur.Role, ResourceAction.TemplateVerifier))
            throw new AccesRefuseException("Seul un compte Admin/Qualité peut vérifier un template.");

        var template = await _templates.ObtenirParIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"Template {templateId} introuvable.");

        template.Verifier(acteur.Id);
        await _templates.MettreAJourAsync(template, ct);
    }
}
