using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class AuthentifierUseCase
{
    private readonly ICompteUtilisateurRepository _comptes;
    private readonly IPasswordHasher _hasher;

    public AuthentifierUseCase(ICompteUtilisateurRepository comptes, IPasswordHasher hasher)
    {
        _comptes = comptes;
        _hasher = hasher;
    }

    public async Task<CompteUtilisateur> ExecuterAsync(string email, string motDePasseEnClair, CancellationToken ct = default)
    {
        var compte = await _comptes.ObtenirParEmailAsync(email, ct);

        if (compte is null || !compte.EstActif || !_hasher.VerifierMotDePasse(motDePasseEnClair, compte.PasswordHash))
            throw new AccesRefuseException("Email ou mot de passe incorrect.");

        return compte;
    }
}
