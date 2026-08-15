using Agirh.Api.Auth;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agirh.Api.Controllers;

public record RegisterRequest(string Email, string MotDePasse);
public record LoginRequest(string Email, string MotDePasse);
public record AuthResponse(string Token, Guid CompteId, string Email, RoleType Role, Guid? PoleId);
public record ElevRoleRequest(Guid CompteId, RoleType NouveauRole, Guid? NouveauPoleId);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly InscrireUseCase _inscrire;
    private readonly AuthentifierUseCase _authentifier;
    private readonly ElevRoleUseCase _elevRole;
    private readonly JwtTokenGenerator _jwtGenerator;
    private readonly ICurrentUserAccessor _currentUser;

    public AuthController(
        InscrireUseCase inscrire,
        AuthentifierUseCase authentifier,
        ElevRoleUseCase elevRole,
        JwtTokenGenerator jwtGenerator,
        ICurrentUserAccessor currentUser)
    {
        _inscrire = inscrire;
        _authentifier = authentifier;
        _elevRole = elevRole;
        _jwtGenerator = jwtGenerator;
        _currentUser = currentUser;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var compte = await _inscrire.ExecuterAsync(request.Email, request.MotDePasse, DateTime.UtcNow, ct);
            var token = _jwtGenerator.GenererToken(compte, DateTime.UtcNow);
            return Ok(new AuthResponse(token, compte.Id, compte.Email, compte.Role, compte.PoleId));
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
            var compte = await _authentifier.ExecuterAsync(request.Email, request.MotDePasse, ct);
            var token = _jwtGenerator.GenererToken(compte, DateTime.UtcNow);
            return Ok(new AuthResponse(token, compte.Id, compte.Email, compte.Role, compte.PoleId));
        }
        catch (AccesRefuseException)
        {
            return Unauthorized("Email ou mot de passe incorrect.");
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Me(CancellationToken ct)
    {
        var acteur = await _currentUser.ObtenirActeurAsync(ct);
        return Ok(new AuthResponse(string.Empty, acteur.Id, acteur.Email, acteur.Role, acteur.PoleId));
    }

    [HttpPost("elever-role")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> ElevRole(ElevRoleRequest request, CancellationToken ct)
    {
        var acteur = await _currentUser.ObtenirActeurAsync(ct);

        try
        {
            var compte = await _elevRole.ExecuterAsync(acteur, request.CompteId, request.NouveauRole, request.NouveauPoleId, ct);
            return Ok(new AuthResponse(string.Empty, compte.Id, compte.Email, compte.Role, compte.PoleId));
        }
        catch (AccesRefuseException)
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
