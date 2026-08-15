using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;

namespace Agirh.Core.UseCases;

public sealed class ArchiverDossierUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly ICollaborateurRepository _collaborateurs;

    public ArchiverDossierUseCase(IWorkflowInstanceRepository instances, ICollaborateurRepository collaborateurs)
    {
        _instances = instances;
        _collaborateurs = collaborateurs;
    }

    public async Task ExecuterAsync(Domain.Entities.CompteUtilisateur acteur, Guid workflowInstanceId, CancellationToken ct = default)
    {
        if (!RbacMatrix.EstAutorise(acteur.Role, ResourceAction.WorkflowInstanceArchiver))
            throw new AccesRefuseException("Seuls RH et Admin/Qualité peuvent archiver un dossier.");

        var instance = await _instances.ObtenirParIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowInstanceId} introuvable.");

        var collaborateur = await _collaborateurs.ObtenirParIdAsync(instance.CollaborateurId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {instance.CollaborateurId} introuvable.");

        if (!PoleScopeGuard.PeutAccederAuCollaborateur(acteur, collaborateur))
            throw new AccesRefuseException("Un RH ne peut archiver un dossier que pour un collaborateur de son pôle.");

        instance.Archiver();
        await _instances.MettreAJourAsync(instance, ct);
    }
}
