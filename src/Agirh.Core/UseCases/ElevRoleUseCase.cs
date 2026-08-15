using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class ElevRoleUseCase
{
    private readonly ICompteUtilisateurRepository _comptes;

    public ElevRoleUseCase(ICompteUtilisateurRepository comptes)
    {
        _comptes = comptes;
    }

    public async Task<CompteUtilisateur> ExecuterAsync(
        CompteUtilisateur acteur,
        Guid compteCibleId,
        RoleType nouveauRole,
        Guid? nouveauPoleId,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.EstAutorise(acteur.Role, ResourceAction.CompteElevRole))
            throw new AccesRefuseException("Seul un compte Admin/Qualité peut élever un rôle.");

        var cible = await _comptes.ObtenirParIdAsync(compteCibleId, ct)
            ?? throw new InvalidOperationException($"Compte {compteCibleId} introuvable.");

        cible.ElevRole(nouveauRole, nouveauPoleId);
        await _comptes.MettreAJourAsync(cible, ct);
        return cible;
    }
}
