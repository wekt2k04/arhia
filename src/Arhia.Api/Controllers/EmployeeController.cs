using Arhia.Api.Auth;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Arhia.Domain;
using Arhia.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arhia.Api.Controllers;

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
    DateTime StartDate,
    DateTime? DepartureDate = null);

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeeController : ControllerBase
{
    private readonly CreateEmployeeRecordUseCase _createRecord;
    private readonly GetEmployeeUseCase _getEmployee;
    private readonly ListEmployeesUseCase _listEmployees;
    private readonly ICurrentUserAccessor _currentUser;

    public EmployeeController(
        CreateEmployeeRecordUseCase createRecord,
        GetEmployeeUseCase getEmployee,
        ListEmployeesUseCase listEmployees,
        ICurrentUserAccessor currentUser)
    {
        _createRecord = createRecord;
        _getEmployee = getEmployee;
        _listEmployees = listEmployees;
        _currentUser = currentUser;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeResponse>> GetById(Guid id, CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);
        try
        {
            var employee = await _getEmployee.ExecuteAsync(actor, id, ct);
            return Ok(ToResponse(employee));
        }
        catch (AccessDeniedException) { return Forbid(); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeResponse>>> List(CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);
        try
        {
            var employees = await _listEmployees.ExecuteAsync(actor, ct);
            return Ok(employees.Select(ToResponse).ToList());
        }
        catch (AccessDeniedException) { return Forbid(); }
    }

    private static EmployeeResponse ToResponse(Domain.Entities.Employee employee) => new(
        employee.Id, employee.EmployeeNumber.Value, employee.LastName, employee.FirstName,
        employee.JobTitle, employee.DepartmentId, employee.ContractType, employee.StartDate,
        employee.DepartureDate);

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
