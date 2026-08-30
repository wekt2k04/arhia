using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Domain;
using Arhia.Domain.Entities;

namespace Arhia.Core.UseCases;

public sealed class RegisterUseCase
{
    private readonly IUserAccountRepository _accounts;
    private readonly IPasswordHasher _hasher;

    public RegisterUseCase(IUserAccountRepository accounts, IPasswordHasher hasher)
    {
        _accounts = accounts;
        _hasher = hasher;
    }

    public async Task<UserAccount> ExecuteAsync(
        string email,
        string plainTextPassword,
        DateTime createdAt,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plainTextPassword) || plainTextPassword.Length < 8)
            throw new ArgumentException("Le mot de passe doit contenir au moins 8 caractères.", nameof(plainTextPassword));

        var existing = await _accounts.GetByEmailAsync(email, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Un compte existe déjà pour {email}.");

        var hash = _hasher.HashPassword(plainTextPassword);

        // Auto-inscription → toujours rôle Employee, jamais élevé à l'inscription (docs/LOGIQUE_METIER.md §1)
        var account = new UserAccount(Guid.NewGuid(), email, hash, RoleType.Employee, null, createdAt);

        await _accounts.AddAsync(account, ct);
        return account;
    }
}
