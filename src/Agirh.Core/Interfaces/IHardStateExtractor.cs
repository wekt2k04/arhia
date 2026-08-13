using System.Security.Claims;

namespace Agirh.Core.Interfaces;

/// <summary>
/// Zone Rouge (Hard State) — extraite EXCLUSIVEMENT du JWT par le code C#.
/// Inaltérable par l'IA. Aucun LLM ne passe par ce struct.
/// </summary>
public readonly record struct HardState
{
    public required string UserId { get; init; }
    public required string Role { get; init; }
    public required RoleFlags RoleFlag { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public Guid? ManagerId { get; init; }
    public bool IsActive { get; init; }

    public string DisplayName => $"{FirstName} {LastName}";
}

/// <summary>
/// Pont Zero-Trust : seul ce service extrait le HardState du ClaimsPrincipal JWT.
/// Tout appel à ce service doit être encapsulé dans du code C# — jamais exposé à l'IA.
/// </summary>
public interface IHardStateExtractor
{
    /// <exception cref="InvalidOperationException">Si l'utilisateur n'est pas authentifié</exception>
    HardState ExtractFromPrincipal(ClaimsPrincipal principal);
}
