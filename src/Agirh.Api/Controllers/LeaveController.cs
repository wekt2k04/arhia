using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;

namespace Agirh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaveController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveController> _logger;

    public LeaveController(IUnitOfWork unitOfWork, ILogger<LeaveController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<IActionResult> GetByEmployee(Guid employeeId)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

        if (currentRole == "Collaborator" && employeeId != currentUserId)
            return Forbid();

        var leaves = await _unitOfWork.LeaveRequests.GetByEmployeeIdAsync(employeeId);
        return Ok(leaves.Select(l => new
        {
            l.Id,
            l.EmployeeId,
            l.StartDate,
            l.EndDate,
            l.Status,
            l.Type,
            l.DaysRequested,
            l.ApprovedById,
            l.CreatedAt
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeaveDto dto, CancellationToken ct = default)
    {
        if (dto == null)
            return BadRequest(new { Message = "Données requises" });

        if (dto.EndDate <= dto.StartDate)
            return BadRequest(new { Message = "La date de fin doit être après la date de début" });

        if (dto.DaysRequested <= 0)
            return BadRequest(new { Message = "Le nombre de jours doit être positif" });

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

        var leave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = currentRole == "Collaborator" ? currentUserId : (dto.EmployeeId ?? currentUserId),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Type = dto.Type ?? "Conges",
            DaysRequested = dto.DaysRequested,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        var validTypes = new[] { "Conges", "CET", "Maladie" };
        if (!validTypes.Contains(leave.Type))
            return BadRequest(new { Message = $"Type invalide. Types acceptés: {string.Join(", ", validTypes)}" });

        await _unitOfWork.LeaveRequests.AddAsync(leave);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Leave request created: {Id} for Employee {EmployeeId}", leave.Id, leave.EmployeeId);

        return CreatedAtAction(nameof(GetByEmployee), new { employeeId = leave.EmployeeId }, new
        {
            leave.Id,
            leave.EmployeeId,
            leave.StartDate,
            leave.EndDate,
            leave.Status,
            leave.Type,
            leave.DaysRequested,
            leave.CreatedAt
        });
    }

    [HttpPut("{id:guid}/approve")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequestDto dto, CancellationToken ct = default)
    {
        if (dto == null)
            return BadRequest(new { Message = "Données requises" });

        var leave = await _unitOfWork.LeaveRequests.GetByIdAsync(id);
        if (leave == null) return NotFound();

        if (leave.Status != "Pending")
            return BadRequest(new { Message = $"La demande est déjà {leave.Status}. Impossible de la modifier." });

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

        if (currentRole == "Manager")
        {
            var employee = await _unitOfWork.Employees.GetByIdAsync(leave.EmployeeId);
            if (employee?.ManagerId != currentUserId)
                return Forbid();
        }

        leave.Status = dto.Approved ? "Approved" : "Rejected";
        leave.ApprovedById = currentUserId;

        await _unitOfWork.LeaveRequests.UpdateAsync(leave);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Leave {Id} {Status} by {UserId}", id, leave.Status, currentUserId);

        return Ok(new { leave.Id, leave.Status, leave.ApprovedById });
    }
}

public class CreateLeaveDto
{
    public Guid? EmployeeId { get; set; }

    [Required(ErrorMessage = "Date de début requise")]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "Date de fin requise")]
    public DateTime EndDate { get; set; }

    [Required(ErrorMessage = "Nombre de jours requis")]
    [Range(0.5, 365, ErrorMessage = "Le nombre de jours doit être entre 0.5 et 365")]
    public decimal DaysRequested { get; set; }

    public string? Type { get; set; }
}

public class ApproveRequestDto
{
    [Required]
    public bool Approved { get; set; }
}
