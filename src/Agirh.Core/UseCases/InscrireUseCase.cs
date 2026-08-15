using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class InscrireUseCase
{
    private readonly ICompteUtilisateurRepository _comptes;
    private readonly IPasswordHasher _hasher;

    public InscrireUseCase(ICompteUtilisateurRepository comptes, IPasswordHasher hasher)
    {
        _comptes = comptes;
        _hasher = hasher;
    }

    public async Task<CompteUtilisateur> ExecuterAsync(
        string email,
        string motDePasseEnClair,
        DateTime dateCreation,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motDePasseEnClair) || motDePasseEnClair.Length < 8)
            throw new ArgumentException("Le mot de passe doit contenir au moins 8 caractères.", nameof(motDePasseEnClair));

        var existant = await _comptes.ObtenirParEmailAsync(email, ct);
        if (existant is not null)
            throw new InvalidOperationException($"Un compte existe déjà pour {email}.");

        var hash = _hasher.HacherMotDePasse(motDePasseEnClair);

        // Auto-inscription → toujours rôle Collaborateur, jamais élevé à l'inscription (LOGIQUE_METIER.md §1)
        var compte = new CompteUtilisateur(Guid.NewGuid(), email, hash, RoleType.Collaborateur, null, dateCreation);

        await _comptes.AjouterAsync(compte, ct);
        return compte;
    }
}
