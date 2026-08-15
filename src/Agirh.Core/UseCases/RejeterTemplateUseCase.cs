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

    public async Task ExecuterAsync(CompteUtilisateur acteur, Guid templateId, string motif, CancellationToken ct = default)
    {
        if (!RbacMatrix.EstAutorise(acteur.Role, ResourceAction.TemplateRejeter))
            throw new AccesRefuseException("Seul un compte Admin/Qualité peut rejeter un template.");

        var template = await _templates.ObtenirParIdAsync(templateId, ct)
            ?? throw new InvalidOperationException($"Template {templateId} introuvable.");

        template.Rejeter(acteur.Id, motif);
        await _templates.MettreAJourAsync(template, ct);
    }
}
