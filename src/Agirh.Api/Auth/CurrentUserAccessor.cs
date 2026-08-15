using System.Security.Claims;
using Agirh.Core.Ports;
using Agirh.Domain.Entities;

namespace Agirh.Api.Auth;

public interface ICurrentUserAccessor
{
    Task<CompteUtilisateur> ObtenirActeurAsync(CancellationToken ct = default);
}

public class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICompteUtilisateurRepository _comptes;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor, ICompteUtilisateurRepository comptes)
    {
        _httpContextAccessor = httpContextAccessor;
        _comptes = comptes;
    }

    public async Task<CompteUtilisateur> ObtenirActeurAsync(CancellationToken ct = default)
    {
        var user = _httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("Aucun contexte HTTP disponible.");

        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("Token sans identifiant d'acteur.");

        if (!Guid.TryParse(idClaim, out var acteurId))
            throw new UnauthorizedAccessException("Identifiant d'acteur invalide dans le token.");

        return await _comptes.ObtenirParIdAsync(acteurId, ct)
            ?? throw new UnauthorizedAccessException("Le compte associé à ce token n'existe plus.");
    }
}
