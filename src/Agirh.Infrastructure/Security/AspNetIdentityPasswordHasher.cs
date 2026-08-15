using Agirh.Core.Ports;
using Agirh.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Agirh.Infrastructure.Security;

public class AspNetIdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<CompteUtilisateur> _hasher = new();

    public string HacherMotDePasse(string motDePasseEnClair) =>
        _hasher.HashPassword(null!, motDePasseEnClair);

    public bool VerifierMotDePasse(string motDePasseEnClair, string hash) =>
        _hasher.VerifyHashedPassword(null!, hash, motDePasseEnClair) != PasswordVerificationResult.Failed;
}
