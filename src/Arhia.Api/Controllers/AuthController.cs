using Arhia.Api.Auth;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Arhia.Domain;
using Arhia.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arhia.Api.Controllers;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, Guid AccountId, string Email, RoleType Role, Guid? DepartmentId);
public record ElevateRoleRequest(Guid AccountId, RoleType NewRole, Guid? NewDepartmentId);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RegisterUseCase _register;
    private readonly AuthenticateUseCase _authenticate;
    private readonly ElevateRoleUseCase _elevateRole;
    private readonly JwtTokenGenerator _jwtGenerator;
    private readonly ICurrentUserAccessor _currentUser;

    public AuthController(
        RegisterUseCase register,
        AuthenticateUseCase authenticate,
        ElevateRoleUseCase elevateRole,
        JwtTokenGenerator jwtGenerator,
        ICurrentUserAccessor currentUser)
    {
        _register = register;
        _authenticate = authenticate;
        _elevateRole = elevateRole;
        _jwtGenerator = jwtGenerator;
        _currentUser = currentUser;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var account = await _register.ExecuteAsync(request.Email, request.Password, DateTime.UtcNow, ct);
            var token = _jwtGenerator.GenerateToken(account, DateTime.UtcNow);
            return Ok(new AuthResponse(token, account.Id, account.Email, account.Role, account.DepartmentId));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            var account = await _authenticate.ExecuteAsync(request.Email, request.Password, ct);
            var token = _jwtGenerator.GenerateToken(account, DateTime.UtcNow);
            return Ok(new AuthResponse(token, account.Id, account.Email, account.Role, account.DepartmentId));
        }
        catch (AccessDeniedException)
        {
            return Unauthorized("Email ou mot de passe incorrect.");
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Me(CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);
        return Ok(new AuthResponse(string.Empty, actor.Id, actor.Email, actor.Role, actor.DepartmentId));
    }

    [HttpPost("elevate-role")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> ElevateRole(ElevateRoleRequest request, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            var account = await _elevateRole.ExecuteAsync(actor, request.AccountId, request.NewRole, request.NewDepartmentId, ct);
            return Ok(new AuthResponse(string.Empty, account.Id, account.Email, account.Role, account.DepartmentId));
        }
        catch (AccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
