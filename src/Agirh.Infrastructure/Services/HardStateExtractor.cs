using System.Security.Claims;
using Agirh.Core.Interfaces;

namespace Agirh.Infrastructure.Services;

public sealed class HardStateExtractor : IHardStateExtractor
{
    public HardState ExtractFromPrincipal(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("User ID (NameIdentifier) not found in JWT.");

        var roleStr = principal.FindFirstValue(ClaimTypes.Role) ?? "Collaborator";
        var roleFlag = roleStr.ToLowerInvariant() switch
        {
            "admin" => RoleFlags.Admin,
            "manager" => RoleFlags.Manager,
            _ => RoleFlags.Collaborator,
        };

        return new HardState
        {
            UserId = userId,
            Role = roleStr,
            RoleFlag = roleFlag,
            Email = principal.FindFirstValue(ClaimTypes.Email) ?? "",
            FirstName = principal.FindFirstValue(ClaimTypes.GivenName) ?? "",
            LastName = principal.FindFirstValue(ClaimTypes.Surname) ?? "",
            ManagerId = principal.FindFirstValue("managerId") is { Length: > 0 } mgr && Guid.TryParse(mgr, out var mgrGuid) ? mgrGuid : null,
            IsActive = principal.FindFirstValue("is_active")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false,
        };
    }
}
