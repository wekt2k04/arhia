using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class CloturerDossierUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly ICollaborateurRepository _collaborateurs;

    public CloturerDossierUseCase(IWorkflowInstanceRepository instances, ICollaborateurRepository collaborateurs)
    {
        _instances = instances;
        _collaborateurs = collaborateurs;
    }

    public async Task ExecuterAsync(CompteUtilisateur acteur, Guid workflowInstanceId, DateTime dateCloture, CancellationToken ct = default)
    {
        if (!RbacMatrix.EstAutorise(acteur.Role, ResourceAction.WorkflowInstanceCloturer))
            throw new AccesRefuseException("Seul un RH peut clôturer un dossier.");

        var instance = await _instances.ObtenirParIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowInstanceId} introuvable.");

        var collaborateur = await _collaborateurs.ObtenirParIdAsync(instance.CollaborateurId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {instance.CollaborateurId} introuvable.");

        if (!PoleScopeGuard.PeutAccederAuCollaborateur(acteur, collaborateur))
            throw new AccesRefuseException("Un RH ne peut clôturer un dossier que pour un collaborateur de son pôle.");

        instance.Cloturer(dateCloture);
        await _instances.MettreAJourAsync(instance, ct);
    }
}
