using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Agirh.Api.Dtos;
using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;

namespace Agirh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public EmployeesController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var employees = await _unitOfWork.Employees.GetAllAsync(page, pageSize);

        return Ok(new
        {
            Page = page,
            PageSize = pageSize,
            Data = employees.Select(e => new EmployeeResponseDto(
                e.Id, e.FirstName, e.LastName, e.Email, e.Role, e.IsActive, 0, 0))
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var currentUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var currentRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

        if (currentRole == "Collaborator" && id != currentUserId)
            return Forbid();

        var employee = await _unitOfWork.Employees.GetByIdAsync(id);
        if (employee == null) return NotFound();

        return Ok(new EmployeeResponseDto(
            employee.Id, employee.FirstName, employee.LastName,
            employee.Email, employee.Role, employee.IsActive,
            employee.LeaveBalance, employee.CompteEpargneTemps));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDto dto, CancellationToken ct = default)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            Role = dto.Role ?? "Collaborator",
            IsActive = true,
            LeaveBalance = 0,
            CompteEpargneTemps = 0
        };

        var validRoles = new[] { "Collaborator", "Manager", "Admin" };
        if (!validRoles.Contains(employee.Role))
            return BadRequest(new { Message = $"Rôle invalide. Rôles acceptés: {string.Join(", ", validRoles)}" });

        await _unitOfWork.Employees.AddAsync(employee);
        await _unitOfWork.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = employee.Id }, new EmployeeResponseDto(
            employee.Id, employee.FirstName, employee.LastName,
            employee.Email, employee.Role, employee.IsActive, 0, 0));
    }
}

public class CreateEmployeeDto
{
    [Required(ErrorMessage = "Prénom requis")]
    [StringLength(100, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nom requis")]
    [StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    public string Email { get; set; } = string.Empty;

    public string? Role { get; set; }
}
