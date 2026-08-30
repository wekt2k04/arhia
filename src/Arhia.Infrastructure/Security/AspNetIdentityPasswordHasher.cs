using Arhia.Core.Ports;
using Arhia.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Arhia.Infrastructure.Security;

public class AspNetIdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public string HashPassword(string plainTextPassword) =>
        _hasher.HashPassword(null!, plainTextPassword);

    public bool VerifyPassword(string plainTextPassword, string hash) =>
        _hasher.VerifyHashedPassword(null!, hash, plainTextPassword) != PasswordVerificationResult.Failed;
}
