using System.Security.Claims;
using Agirh.Api.Controllers;
using Agirh.Api.Dtos;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Data;
using Agirh.Infrastructure.Repositories;
using Agirh.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agirh.Tests.Unit;

/// <summary>
/// Behavior tests for <see cref="SalaryAdvanceController.GetById"/> (Phase 5 —
/// endpoint sécurisé GET /api/salary-advance/{id}).
///
/// The controller is instantiated directly with a constructed ClaimsPrincipal
/// and the REAL <see cref="HardStateExtractor"/> (claims NameIdentifier, Role,
/// is_active, managerId), so the identity extraction is production code — the
/// only seam mocked away is the HTTP layer.
///
/// Zero-Trust invariants verified:
///  - IDOR : tout ce qui n'est pas visible renvoie 404 (jamais 403) — anti-énumération ;
///  - Admin exempté des scopes ;
///  - compte inactif → 403 (ordre Dispatcher : IsActive d'abord) ;
///  - NameIdentifier manquant → 401 ;
///  - propriétaire introuvable → 404 (fail-closed).
/// </summary>
public class SalaryAdvanceControllerTests
{
    // --------------------------------------------------------------------- //
    // Given — deterministic harness
    // --------------------------------------------------------------------- //

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal BuildPrincipal(
        string? userId,
        string role,
        bool isActive = true)
    {
        var claims = new List<Claim>();
        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        claims.Add(new Claim(ClaimTypes.Role, role));
        claims.Add(new Claim("is_active", isActive ? "true" : "false"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test-auth"));
    }

    private static SalaryAdvanceController BuildController(AppDbContext context, ClaimsPrincipal principal)
    {
        var unitOfWork = new UnitOfWork(
            context,
            new EmployeeRepository(context),
            new LeaveRequestRepository(context),
            new PayrollProfileRepository(context),
            new SalaryAdvanceRepository(context),
            new AgentConversationRepository(context));

        var controller = new SalaryAdvanceController(
            new HardStateExtractor(), // REAL extractor — reads JWT claims
            unitOfWork,
            NullLogger<SalaryAdvanceController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
        return controller;
    }

    private static async Task<Employee> AddEmployeeAsync(
        AppDbContext context,
        string firstName,
        string lastName,
        string role = "Collaborator",
        Guid? managerId = null)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}.{Guid.NewGuid():N}@agirh.test",
            Role = role,
            IsActive = true,
            ManagerId = managerId,
        };

        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        return employee;
    }

    private static async Task<SalaryAdvanceRequest> AddSalaryAdvanceAsync(
        AppDbContext context,
        Guid employeeId,
        string status,
        decimal amount,
        DateTime requestDate)
    {
        var request = new SalaryAdvanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            AmountRequested = amount,
            Status = status,
            RequestDate = requestDate,
        };

        context.SalaryAdvanceRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }

    private static async Task<SalaryAdvanceResponseDto> ActGetOkAsync(SalaryAdvanceController controller, Guid id)
    {
        var result = await controller.GetById(id, CancellationToken.None);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeOfType<SalaryAdvanceResponseDto>().Subject;
    }

    private static async Task ActGetNotFoundAsync(SalaryAdvanceController controller, Guid id)
    {
        var result = await controller.GetById(id, CancellationToken.None);
        result.Should().BeOfType<NotFoundResult>();
    }

    // ===================================================================== //
    // 5.1 — Collaborator owner: 200 + exact DTO
    // ===================================================================== //

    [Fact]
    public async Task GetById_Should_Return_200_With_Dto_For_Owner_Collaborator()
    {
        // Given an employee who owns a Pending advance request
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont", role: "Collaborator");
        var requestDate = new DateTime(2026, 7, 1, 10, 30, 0, DateTimeKind.Utc);
        var request = await AddSalaryAdvanceAsync(context, employee.Id, status: "Pending", amount: 1250.50m, requestDate);
        var controller = BuildController(context, BuildPrincipal(employee.Id.ToString(), "Collaborator"));

        // When the owner reads their own request
        var dto = await ActGetOkAsync(controller, request.Id);

        // Then every DTO field matches the persisted entity
        dto.Id.Should().Be(request.Id);
        dto.EmployeeId.Should().Be(employee.Id);
        dto.AmountRequested.Should().Be(1250.50m);
        dto.Status.Should().Be("Pending");
        dto.RequestDate.Should().Be(requestDate);
    }

    // ===================================================================== //
    // 5.2 — Collaborator targeting ANOTHER employee's request: 404 (IDOR)
    // ===================================================================== //

    [Fact]
    public async Task GetById_Should_Return_404_And_Keep_Row_When_Collaborator_Targets_Another_Employees_Request()
    {
        // Given a request owned by Marie
        var context = CreateContext();
        var jean = await AddEmployeeAsync(context, "Jean", "Dupont", role: "Collaborator");
        var marie = await AddEmployeeAsync(context, "Marie", "Curie", role: "Collaborator");
        var request = await AddSalaryAdvanceAsync(context, marie.Id, status: "Approved", amount: 900m, DateTime.UtcNow);
        var controller = BuildController(context, BuildPrincipal(jean.Id.ToString(), "Collaborator"));

        // When Jean (not the owner) asks for it by id
        await ActGetNotFoundAsync(controller, request.Id);

        // Then 404 — invisible, no existence leak AND the row is NOT deleted
        context.SalaryAdvanceRequests.Count(r => r.Id == request.Id).Should().Be(1);
    }

    // ===================================================================== //
    // 5.3 — Manager scope: team member => 200, own request => 200
    // ===================================================================== //

    [Fact]
    public async Task GetById_Should_Return_200_For_Manager_On_Team_Member_Request()
    {
        // Given a manager whose subordinate owns an advance request
        var context = CreateContext();
        var manager = await AddEmployeeAsync(context, "Alice", "Martin", role: "Manager");
        var teamMember = await AddEmployeeAsync(context, "Bob", "Durand", role: "Collaborator", managerId: manager.Id);
        var request = await AddSalaryAdvanceAsync(context, teamMember.Id, status: "Pending", amount: 2000m, DateTime.UtcNow);
        var controller = BuildController(context, BuildPrincipal(manager.Id.ToString(), "Manager"));

        // When the manager reads the subordinate's request
        var dto = await ActGetOkAsync(controller, request.Id);

        // Then 200 with the subordinate's request
        dto.Id.Should().Be(request.Id);
        dto.EmployeeId.Should().Be(teamMember.Id);
    }

    [Fact]
    public async Task GetById_Should_Return_200_For_Manager_On_Own_Request()
    {
        // Given a manager's own advance request
        var context = CreateContext();
        var manager = await AddEmployeeAsync(context, "Alice", "Martin", role: "Manager");
        var request = await AddSalaryAdvanceAsync(context, manager.Id, status: "Rejected", amount: 500m, DateTime.UtcNow);
        var controller = BuildController(context, BuildPrincipal(manager.Id.ToString(), "Manager"));

        // When the manager reads their own request
        var dto = await ActGetOkAsync(controller, request.Id);

        // Then 200 with their own request
        dto.Id.Should().Be(request.Id);
        dto.EmployeeId.Should().Be(manager.Id);
        dto.Status.Should().Be("Rejected");
    }

    // ===================================================================== //
    // 5.4 — Manager outside the team: 404 (anti-énumération)
    // ===================================================================== //

    [Fact]
    public async Task GetById_Should_Return_404_For_Manager_Outside_Team()
    {
        // Given an owner whose manager is someone else than the caller
        var context = CreateContext();
        var otherManager = await AddEmployeeAsync(context, "Claire", "Bernard", role: "Manager");
        var owner = await AddEmployeeAsync(context, "David", "Petit", role: "Collaborator", managerId: otherManager.Id);
        var request = await AddSalaryAdvanceAsync(context, owner.Id, status: "Approved", amount: 1500m, DateTime.UtcNow);
        var outsideManager = await AddEmployeeAsync(context, "Alice", "Martin", role: "Manager");
        var controller = BuildController(context, BuildPrincipal(outsideManager.Id.ToString(), "Manager"));

        // When a manager from ANOTHER team reads it
        await ActGetNotFoundAsync(controller, request.Id);

        // Then 404 — invisible AND the row is untouched
        context.SalaryAdvanceRequests.Count(r => r.Id == request.Id).Should().Be(1);
    }

    // ===================================================================== //
    // 5.5 — Admin exempted from every scope: 200 on any request
    // ===================================================================== //

    [Fact]
    public async Task GetById_Should_Return_200_For_Admin_On_Any_Request()
    {
        // Given a request owned by an employee of another manager
        var context = CreateContext();
        var otherManager = await AddEmployeeAsync(context, "Claire", "Bernard", role: "Manager");
        var owner = await AddEmployeeAsync(context, "David", "Petit", role: "Collaborator", managerId: otherManager.Id);
        var request = await AddSalaryAdvanceAsync(context, owner.Id, status: "Pending", amount: 3000m, DateTime.UtcNow);
        var controller = BuildController(context, BuildPrincipal(Guid.NewGuid().ToString(), "Admin"));

        // When an admin reads it (even though the caller is neither owner nor manager)
        var dto = await ActGetOkAsync(controller, request.Id);

        // Then 200 — the admin exemption bypasses the team scope
        dto.Id.Should().Be(request.Id);
        dto.EmployeeId.Should().Be(owner.Id);
    }

    // ===================================================================== //
    // 5.6 — Nonexistent id: 404
    // ===================================================================== //

    [Fact]
    public async Task GetById_Should_Return_404_When_Request_Does_Not_Exist()
    {
        // Given an empty store
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont", role: "Collaborator");
        var controller = BuildController(context, BuildPrincipal(employee.Id.ToString(), "Collaborator"));

        // When asking for a random id
        await ActGetNotFoundAsync(controller, Guid.NewGuid());
    }

    // ===================================================================== //
    // 5.7 — Fail-closed: inactive account => 403 (ordre Dispatcher IsActive)
    // ===================================================================== //

    [Fact]
    public async Task GetById_Should_Return_403_When_Account_Is_Inactive()
    {
        // Given an employee with an advance request but a deactivated account
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont", role: "Collaborator");
        var request = await AddSalaryAdvanceAsync(context, employee.Id, status: "Pending", amount: 1000m, DateTime.UtcNow);
        var controller = BuildController(context, BuildPrincipal(employee.Id.ToString(), "Collaborator", isActive: false));

        // When the inactive owner reads their own request
        var result = await controller.GetById(request.Id, CancellationToken.None);

        // Then 403 — account-level refusal takes precedence
        result.Should().BeOfType<ForbidResult>();
    }

    // ===================================================================== //
    // 5.8 — Fail-closed: missing NameIdentifier => 401
    // ===================================================================== //

    [Fact]
    public async Task GetById_Should_Return_401_When_NameIdentifier_Is_Missing()
    {
        // Given a principal WITHOUT the NameIdentifier claim
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont", role: "Collaborator");
        var request = await AddSalaryAdvanceAsync(context, employee.Id, status: "Pending", amount: 1000m, DateTime.UtcNow);
        var controller = BuildController(context, BuildPrincipal(userId: null, role: "Collaborator"));

        // When the request is attempted
        var result = await controller.GetById(request.Id, CancellationToken.None);

        // Then 401 — the HardState cannot be extracted, nothing else is attempted
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ===================================================================== //
    // 5.9 — Fail-closed: request owner employee does not exist => 404
    // ===================================================================== //

    [Fact]
    public async Task GetById_Should_Return_404_When_Request_Owner_Employee_Is_Missing()
    {
        // Given an orphan advance request (EmployeeId points to NO employee row)
        var context = CreateContext();
        var manager = await AddEmployeeAsync(context, "Alice", "Martin", role: "Manager");
        var orphanRequest = await AddSalaryAdvanceAsync(
            context, Guid.NewGuid(), status: "Pending", amount: 1000m, DateTime.UtcNow);
        var controller = BuildController(context, BuildPrincipal(manager.Id.ToString(), "Manager"));

        // When a manager reads it (the owner cannot be resolved)
        await ActGetNotFoundAsync(controller, orphanRequest.Id);

        // Then 404 — fail-closed, never an escalation to a default owner
        context.SalaryAdvanceRequests.Count(r => r.Id == orphanRequest.Id).Should().Be(1);
    }
}
