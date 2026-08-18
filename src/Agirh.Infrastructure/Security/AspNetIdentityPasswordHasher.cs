using Agirh.Core.Ports;
using Agirh.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Agirh.Infrastructure.Security;

public class AspNetIdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public string HashPassword(string plainTextPassword) =>
        _hasher.HashPassword(null!, plainTextPassword);

    public bool VerifyPassword(string plainTextPassword, string hash) =>
        _hasher.VerifyHashedPassword(null!, hash, plainTextPassword) != PasswordVerificationResult.Failed;
}
