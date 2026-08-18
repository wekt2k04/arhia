using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Core.Security;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

public sealed class AuthenticateUseCase
{
    private readonly IUserAccountRepository _accounts;
    private readonly IPasswordHasher _hasher;

    public AuthenticateUseCase(IUserAccountRepository accounts, IPasswordHasher hasher)
    {
        _accounts = accounts;
        _hasher = hasher;
    }

    public async Task<UserAccount> ExecuteAsync(string email, string plainTextPassword, CancellationToken ct = default)
    {
        var account = await _accounts.GetByEmailAsync(email, ct);

        if (account is null || !account.IsActive || !_hasher.VerifyPassword(plainTextPassword, account.PasswordHash))
            throw new AccessDeniedException("Email ou mot de passe incorrect.");

        return account;
    }
}
