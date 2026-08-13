using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Agirh.Core.Interfaces;
using Agirh.Domain.Interfaces;

namespace Agirh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IJwtTokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IEmployeeRepository employeeRepo, IJwtTokenService tokenService, ILogger<AuthController> logger)
    {
        _employeeRepo = employeeRepo;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { Message = "Email requis" });

        if (!new EmailAddressAttribute().IsValid(request.Email))
            return BadRequest(new { Message = "Format d'email invalide" });

        var email = request.Email.Trim().ToLowerInvariant();
        var employee = await _employeeRepo.GetByEmailAsync(email);

        if (employee == null || !employee.IsActive)
        {
            _logger.LogWarning("Login failed for {Email}: employee not found or inactive", email);
            return Unauthorized(new { Message = "Email invalide ou compte inactif" });
        }

        if (string.IsNullOrEmpty(employee.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, employee.PasswordHash))
        {
            _logger.LogWarning("Login failed for {Email}: invalid password", email);
            return Unauthorized(new { Message = "Email ou mot de passe invalide" });
        }

        var token = _tokenService.GenerateToken(employee);
        _logger.LogInformation("User {Email} logged in successfully with role {Role}", email, employee.Role);

        return Ok(new
        {
            Token = token,
            EmployeeId = employee.Id,
            Role = employee.Role,
            FirstName = employee.FirstName,
            LastName = employee.LastName
        });
    }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Email requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    public string Email { get; set; } = string.Empty;

    public string? Password { get; set; }
}
