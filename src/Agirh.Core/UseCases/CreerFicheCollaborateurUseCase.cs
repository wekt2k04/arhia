using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;

namespace Agirh.Core.UseCases;

public sealed class CreerFicheCollaborateurUseCase
{
    private readonly ICollaborateurRepository _collaborateurs;

    public CreerFicheCollaborateurUseCase(ICollaborateurRepository collaborateurs)
    {
        _collaborateurs = collaborateurs;
    }

    public async Task<Collaborateur> ExecuterAsync(
        CompteUtilisateur acteur,
        Matricule matricule,
        string nom,
        string prenom,
        string poste,
        Guid poleId,
        TypeContrat typeContrat,
        DateTime dateIntegration,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.EstAutorise(acteur.Role, ResourceAction.CollaborateurCreer))
            throw new AccesRefuseException("Seul un RH peut créer une fiche collaborateur.");

        if (!PoleScopeGuard.PeutAccederAuPole(acteur, poleId))
            throw new AccesRefuseException("Un RH ne peut créer un collaborateur que dans son propre pôle.");

        var existant = await _collaborateurs.ObtenirParMatriculeAsync(matricule, ct);
        if (existant is not null)
            throw new InvalidOperationException($"Le matricule {matricule} est déjà utilisé.");

        var collaborateur = new Collaborateur(
            Guid.NewGuid(), matricule, nom, prenom, poste, poleId, typeContrat, dateIntegration);

        await _collaborateurs.AjouterAsync(collaborateur, ct);
        return collaborateur;
    }
}
