using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class InstancierWorkflowUseCase
{
    private readonly ICollaborateurRepository _collaborateurs;
    private readonly IWorkflowTemplateRepository _templates;
    private readonly IWorkflowInstanceRepository _instances;

    public InstancierWorkflowUseCase(
        ICollaborateurRepository collaborateurs,
        IWorkflowTemplateRepository templates,
        IWorkflowInstanceRepository instances)
    {
        _collaborateurs = collaborateurs;
        _templates = templates;
        _instances = instances;
    }

    public async Task<WorkflowInstance> ExecuterAsync(
        CompteUtilisateur acteur,
        Guid collaborateurId,
        WorkflowType type,
        DateTime dateCreation,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.EstAutorise(acteur.Role, ResourceAction.WorkflowInstancier))
            throw new AccesRefuseException("Seul un RH peut instancier un workflow.");

        var collaborateur = await _collaborateurs.ObtenirParIdAsync(collaborateurId, ct)
            ?? throw new InvalidOperationException($"Collaborateur {collaborateurId} introuvable.");

        if (!PoleScopeGuard.PeutAccederAuCollaborateur(acteur, collaborateur))
            throw new AccesRefuseException("Un RH ne peut instancier un workflow que pour un collaborateur de son pôle.");

        var template = await _templates.ObtenirDernierApprouveAsync(type, ct)
            ?? throw new InvalidOperationException($"Aucun template approuvé pour le type {type}.");

        var itemsApplicables = template.ResoudreItemsApplicables(collaborateur.TypeContrat);
        var items = itemsApplicables
            .Select(i => new ChecklistItemStatus(Guid.NewGuid(), i.Id, i.Libelle))
            .ToList();

        var instance = new WorkflowInstance(
            Guid.NewGuid(), collaborateur.Id, template.Id, template.Version, type, items, dateCreation);

        await _instances.AjouterAsync(instance, ct);
        return instance;
    }
}
