using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Agirh.Api.Dtos;
using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;

namespace Agirh.Api.Controllers;

[ApiController]
[Route("api/salary-advance")]
[Authorize]
public class SalaryAdvanceController : ControllerBase
{
    private readonly IHardStateExtractor _hardStateExtractor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SalaryAdvanceController> _logger;

    public SalaryAdvanceController(
        IHardStateExtractor hardStateExtractor,
        IUnitOfWork unitOfWork,
        ILogger<SalaryAdvanceController> logger)
    {
        _hardStateExtractor = hardStateExtractor;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        HardState hardState;
        try
        {
            hardState = _hardStateExtractor.ExtractFromPrincipal(User);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("SALARY_ADVANCE_401 — identity extraction failed: {Message}", ex.Message);
            return Unauthorized();
        }

        // Ordre Dispatcher : IsActive d'abord — un compte inactif est refusé
        // globalement (403), même si la demande ciblée existe.
        if (!hardState.IsActive)
        {
            _logger.LogWarning("SALARY_ADVANCE_FORBIDDEN — account {UserId} is inactive", hardState.UserId);
            return Forbid();
        }

        var request = await _unitOfWork.SalaryAdvances.GetByIdAsync(id, ct);
        if (request == null)
            return NotFound();

        if (!await IsVisibleAsync(request, hardState))
        {
            _logger.LogWarning(
                "SALARY_ADVANCE_IDOR — user {UserId} (role {Role}) attempted to read advance {AdvanceId} of employee {EmployeeId} (denied)",
                hardState.UserId, hardState.Role, id, request.EmployeeId);
            // Anti-énumération : tout ce qui n'est pas visible renvoie 404,
            // jamais 403, pour ne pas révéler l'existence de la ressource.
            return NotFound();
        }

        return Ok(new SalaryAdvanceResponseDto
        {
            Id = request.Id,
            AmountRequested = request.AmountRequested,
            Status = request.Status,
            RequestDate = request.RequestDate,
            EmployeeId = request.EmployeeId,
        });
    }

    private async Task<bool> IsVisibleAsync(SalaryAdvanceRequest request, HardState identity)
    {
        // Admin : exempté de TOUS les scopes.
        if (identity.RoleFlag == RoleFlags.Admin)
            return true;

        var callerId = ToGuidOrNull(identity.UserId);

        // Collaborateur : uniquement ses propres demandes.
        if (identity.RoleFlag == RoleFlags.Collaborator)
            return callerId.HasValue && request.EmployeeId == callerId.Value;

        // Manager : sa propre demande OU la demande d'un membre de son équipe.
        if (callerId.HasValue && request.EmployeeId == callerId.Value)
            return true;

        var owner = await _unitOfWork.Employees.GetByIdAsync(request.EmployeeId);
        if (owner == null)
            return false; // fail-closed : propriétaire introuvable => invisible

        return callerId.HasValue && owner.ManagerId == callerId.Value;
    }

    private static Guid? ToGuidOrNull(string? value) =>
        Guid.TryParse(value, out var guid) ? guid : null;
}
