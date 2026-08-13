using Agirh.Domain.Entities;

namespace Agirh.Core.Interfaces;

/// <summary>
/// Port applicatif : génération de jetons JWT.
/// Déclaré en Core (couche applicative), implémenté en Infrastructure (adaptateur).
/// </summary>
public interface IJwtTokenService
{
    string GenerateToken(Employee employee);
}
