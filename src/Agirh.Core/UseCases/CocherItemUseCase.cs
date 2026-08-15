using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class CocherItemUseCase
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly ICollaborateurRepository _collaborateurs;

    public CocherItemUseCase(IWorkflowInstanceRepository instances, ICollaborateurRepository collaborateurs)
    {
        _instances = instances;
        _collaborateurs = collaborateurs;
    }

    public async Task ExecuterAsync(
        CompteUtilisateur acteur,
        Guid workflowInstanceId,
        Guid itemId,
        ItemEtat etat,
        string? commentaire,
        DateTime dateCoche,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.EstAutorise(acteur.Role, ResourceAction.WorkflowInstanceCocher))
            throw new AccesRefuseException("Seul un RH peut cocher un item de checklist.");

        var instance = await _instances.ObtenirParIdAsync(workflowInstanceId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowInstanceId} introuvable.");

        var collaborateur = await _collaborateurs.ObtenirParIdAsync(instance.CollaborateurId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {instance.CollaborateurId} introuvable.");

        if (!PoleScopeGuard.PeutAccederAuCollaborateur(acteur, collaborateur))
            throw new AccesRefuseException("Un RH ne peut cocher un item que pour un collaborateur de son pôle.");

        instance.Cocher(itemId, etat, acteur.Id, dateCoche, commentaire);
        await _instances.MettreAJourAsync(instance, ct);
    }
}
