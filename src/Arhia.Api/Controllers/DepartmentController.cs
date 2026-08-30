using Arhia.Api.Auth;
using Arhia.Core.Security;
using Arhia.Core.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arhia.Api.Controllers;

public record DepartmentResponse(Guid Id, string Name);

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentController : ControllerBase
{
    private readonly ListDepartmentsUseCase _listDepartments;
    private readonly ICurrentUserAccessor _currentUser;

    public DepartmentController(ListDepartmentsUseCase listDepartments, ICurrentUserAccessor currentUser)
    {
        _listDepartments = listDepartments;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentResponse>>> List(CancellationToken ct)
    {
        var actor = await _currentUser.GetActorAsync(ct);
        try
        {
            var departments = await _listDepartments.ExecuteAsync(actor, ct);
            return Ok(departments.Select(d => new DepartmentResponse(d.Id, d.Name)).ToList());
        }
        catch (AccessDeniedException) { return Forbid(); }
    }
}
