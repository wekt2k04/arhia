using System.Security.Claims;
using Arhia.Core.Ports;
using Arhia.Domain.Entities;

namespace Arhia.Api.Auth;

public interface ICurrentUserAccessor
{
    Task<UserAccount> GetActorAsync(CancellationToken ct = default);
}

public class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserAccountRepository _accounts;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor, IUserAccountRepository accounts)
    {
        _httpContextAccessor = httpContextAccessor;
        _accounts = accounts;
    }

    public async Task<UserAccount> GetActorAsync(CancellationToken ct = default)
    {
        var user = _httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("Aucun contexte HTTP disponible.");

        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("Token sans identifiant d'acteur.");

        if (!Guid.TryParse(idClaim, out var actorId))
            throw new UnauthorizedAccessException("Identifiant d'acteur invalide dans le token.");

        var account = await _accounts.GetByIdAsync(actorId, ct)
            ?? throw new UnauthorizedAccessException("Le compte associé à ce token n'existe plus.");

        if (!account.IsActive)
            throw new UnauthorizedAccessException("Ce compte a été désactivé.");

        return account;
    }
}
