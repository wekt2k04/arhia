using Agirh.Api.Auth;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agirh.Api.Controllers;

public record CreateEmployeeRecordRequest(
    string EmployeeNumber,
    string LastName,
    string FirstName,
    string JobTitle,
    Guid DepartmentId,
    ContractType ContractType,
    DateTime StartDate);

public record EmployeeResponse(
    Guid Id,
    string EmployeeNumber,
    string LastName,
    string FirstName,
    string JobTitle,
    Guid DepartmentId,
    ContractType ContractType,
    DateTime StartDate);

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeeController : ControllerBase
{
    private readonly CreateEmployeeRecordUseCase _createRecord;
    private readonly ICurrentUserAccessor _currentUser;

    public EmployeeController(CreateEmployeeRecordUseCase createRecord, ICurrentUserAccessor currentUser)
    {
        _createRecord = createRecord;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRecordRequest request, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);

        try
        {
            var employee = await _createRecord.ExecuteAsync(
                actor,
                new EmployeeNumber(request.EmployeeNumber),
                request.LastName,
                request.FirstName,
                request.JobTitle,
                request.DepartmentId,
                request.ContractType,
                request.StartDate,
                ct);

            return Ok(new EmployeeResponse(
                employee.Id, employee.EmployeeNumber.Value, employee.LastName, employee.FirstName,
                employee.JobTitle, employee.DepartmentId, employee.ContractType, employee.StartDate));
        }
        catch (AccessDeniedException)
        {
            return Forbid();
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
}
