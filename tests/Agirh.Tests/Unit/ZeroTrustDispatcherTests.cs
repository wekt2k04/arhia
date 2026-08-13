using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Data;
using Agirh.Infrastructure.MAF;
using Agirh.Infrastructure.Repositories;
using Agirh.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agirh.Tests.Unit;

/// <summary>
/// Behavior tests for the REAL <see cref="ZeroTrustDispatcher"/> against a real
/// EF InMemory harness (correctif P0-1 + matrice RBAC + scope manager).
///
/// Given a cognitive vector and a hard-state identity
/// When the dispatcher routes the intention
/// Then RBAC (INSUFFICIENT_ROLE), account state (ACCOUNT_INACTIVE), confidence
/// (IntentUnresolvable), manager scope (SCOPE_MISMATCH) and admin exemption are
/// enforced deterministically — and VALIDATED PreFlight parameters always beat
/// the extracted-entity fallback (P0-1).
/// </summary>
public class ZeroTrustDispatcherTests
{
    // --------------------------------------------------------------------- //
    // Given — deterministic harness
    // --------------------------------------------------------------------- //

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (ZeroTrustDispatcher Dispatcher, AppDbContext Context) BuildHarness()
    {
        var context = CreateContext();
        var employeeRepo = new EmployeeRepository(context);
        var leaveRepo = new LeaveRequestRepository(context);
        var payrollRepo = new PayrollProfileRepository(context);
        var advanceRepo = new SalaryAdvanceRepository(context);
        var unitOfWork = new UnitOfWork(
            context,
            employeeRepo,
            leaveRepo,
            payrollRepo,
            advanceRepo,
            new AgentConversationRepository(context));

        var tools = new IMafTool[]
        {
            new DemanderAvanceSalaireAsync(payrollRepo, advanceRepo, unitOfWork),
            new ConsulterSoldeTool(employeeRepo),
            new RevoquerAccesITTool(employeeRepo),
            new ApprouverDemandeCongesTool(leaveRepo, employeeRepo),
        };

        var dispatcher = new ZeroTrustDispatcher(
            tools,
            employeeRepo,
            leaveRepo,
            NullLogger<ZeroTrustDispatcher>.Instance);

        return (dispatcher, context);
    }

    private static async Task<Employee> AddEmployeeAsync(
        AppDbContext context,
        Guid? managerId = null,
        string role = "Collaborator")
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = Guid.NewGuid().ToString("N")[..8],
            Email = $"dispatcher-{Guid.NewGuid():N}@agirh.test",
            Role = role,
            IsActive = true,
            ManagerId = managerId,
        };
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        return employee;
    }

    private static async Task<LeaveRequest> AddLeaveRequestAsync(AppDbContext context, Guid employeeId)
    {
        var leave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(3),
            Status = "Pending",
            Type = "Conges",
            DaysRequested = 2m,
        };
        context.LeaveRequests.Add(leave);
        await context.SaveChangesAsync();
        return leave;
    }

    private static DynamicContextVector BuildVector(
        ConversationIntention intention,
        IReadOnlyDictionary<string, string?>? entities = null,
        float confidence = 0.9f) => new()
    {
        Intention = intention,
        ConfidenceScore = confidence,
        MainIdea = "demande liée à la conversation",
        Urgency = UrgencyLevel.Low,
        Tone = TonePreference.Professional,
        IsFollowUp = false,
        TriggerPhrase = "",
        ExtractedEntities = entities ?? new Dictionary<string, string?>(),
        ModelUsed = "dispatcher-test",
    };

    private static HardState BuildIdentity(
        Guid userId,
        string role,
        RoleFlags roleFlag,
        bool isActive = true) => new()
    {
        UserId = userId.ToString(),
        Role = role,
        RoleFlag = roleFlag,
        Email = "identity@agirh.test",
        FirstName = "Identity",
        LastName = "Test",
        IsActive = isActive,
    };

    private static DispatchInput BuildInput(DynamicContextVector vector, HardState identity, object? validated = null)
        => new()
        {
            CognitiveContext = vector,
            Identity = identity,
            ValidatedParameters = validated is null
                ? null
                : System.Text.Json.JsonSerializer.SerializeToElement(validated),
        };

    // --------------------------------------------------------------------- //
    // RBAC — the matrix grants RevoquerAccesITAsync to Admin only
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_It_Revocation_When_Role_Is_Collaborator()
    {
        // Given a Collaborator asking to revoke IT access
        var (dispatcher, context) = BuildHarness();
        var target = await AddEmployeeAsync(context);

        // When the dispatcher routes the ITAccessRevocation intention
        var result = await dispatcher.DispatchAsync(BuildInput(
            BuildVector(ConversationIntention.ITAccessRevocation,
                new Dictionary<string, string?> { ["employee_id"] = target.Id.ToString() }),
            BuildIdentity(Guid.NewGuid(), "Collaborator", RoleFlags.Collaborator)));

        // Then the dispatch is rejected with INSUFFICIENT_ROLE and the exact message
        var rejection = result.Should().BeOfType<DispatchRejected>().Subject;
        rejection.Reason.DenialCode.Should().Be(DenialCode.INSUFFICIENT_ROLE);
        rejection.Reason.UserMessage.Should().Be("Seuls les administrateurs peuvent révoquer des accès IT.");
    }

    // --------------------------------------------------------------------- //
    // Account state — inactive accounts are denied before any routing
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_When_Account_Is_Inactive()
    {
        // Given an Admin identity whose account is inactive (fail-closed beats RBAC)
        var (dispatcher, _) = BuildHarness();

        // When the dispatcher routes ANY intention
        var result = await dispatcher.DispatchAsync(BuildInput(
            BuildVector(ConversationIntention.ITAccessRevocation),
            BuildIdentity(Guid.NewGuid(), "Admin", RoleFlags.Admin, isActive: false)));

        // Then the account state is rejected with ACCOUNT_INACTIVE
        var rejection = result.Should().BeOfType<DispatchRejected>().Subject;
        rejection.Reason.DenialCode.Should().Be(DenialCode.ACCOUNT_INACTIVE);
    }

    // --------------------------------------------------------------------- //
    // Confidence threshold — below 0.4 the intent is unresolved
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Return_IntentUnresolvable_When_Confidence_Below_Threshold()
    {
        // Given a low-confidence cognitive vector (0.39 < 0.4)
        var (dispatcher, _) = BuildHarness();

        // When the dispatcher routes it
        var result = await dispatcher.DispatchAsync(BuildInput(
            BuildVector(ConversationIntention.SalaryAdvance, confidence: 0.39f),
            BuildIdentity(Guid.NewGuid(), "Collaborator", RoleFlags.Collaborator)));

        // Then the intent is reported as unresolvable
        result.Should().BeOfType<IntentUnresolvable>();
    }

    // --------------------------------------------------------------------- //
    // Manager scope — the corrected loophole (target with NO manager)
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_Manager_When_Target_Has_No_Manager_And_Is_Not_Self()
    {
        // Given a Manager and a target employee with ManagerId == null (orphan)
        var (dispatcher, context) = BuildHarness();
        var manager = await AddEmployeeAsync(context, role: "Manager");
        var orphan = await AddEmployeeAsync(context, managerId: null);

        // When the Manager targets the orphan (≠ self)
        var result = await dispatcher.DispatchAsync(BuildInput(
            BuildVector(ConversationIntention.LeaveBalance,
                new Dictionary<string, string?> { ["employee_id"] = orphan.Id.ToString() }),
            BuildIdentity(manager.Id, "Manager", RoleFlags.Manager)));

        // Then the orphan is OUT of the manager's team → SCOPE_MISMATCH
        var rejection = result.Should().BeOfType<DispatchRejected>().Subject;
        rejection.Reason.DenialCode.Should().Be(DenialCode.SCOPE_MISMATCH);
    }

    [Fact]
    public async Task Should_Allow_Manager_When_Target_Is_Self()
    {
        // Given a Manager targeting their OWN employee record
        var (dispatcher, context) = BuildHarness();
        var manager = await AddEmployeeAsync(context, role: "Manager");

        // When the Manager queries their own data
        var result = await dispatcher.DispatchAsync(BuildInput(
            BuildVector(ConversationIntention.LeaveBalance,
                new Dictionary<string, string?> { ["employee_id"] = manager.Id.ToString() }),
            BuildIdentity(manager.Id, "Manager", RoleFlags.Manager)));

        // Then the dispatch reaches the tool (self is always in scope)
        var dispatch = result.Should().BeOfType<DispatchToTool>().Subject;
        dispatch.Dispatch.ToolName.Should().Be("ConsulterSoldeAsync");
    }

    [Fact]
    public async Task Should_Allow_Manager_When_Target_Is_A_Subordinate()
    {
        // Given a Manager and a direct subordinate (ManagerId == manager)
        var (dispatcher, context) = BuildHarness();
        var manager = await AddEmployeeAsync(context, role: "Manager");
        var subordinate = await AddEmployeeAsync(context, managerId: manager.Id);

        // When the Manager queries the subordinate
        var result = await dispatcher.DispatchAsync(BuildInput(
            BuildVector(ConversationIntention.LeaveBalance,
                new Dictionary<string, string?> { ["employee_id"] = subordinate.Id.ToString() }),
            BuildIdentity(manager.Id, "Manager", RoleFlags.Manager)));

        // Then the dispatch reaches the tool
        var dispatch = result.Should().BeOfType<DispatchToTool>().Subject;
        dispatch.Dispatch.ToolName.Should().Be("ConsulterSoldeAsync");
    }

    [Fact]
    public async Task Should_Reject_Manager_When_Target_Belongs_To_Another_Manager()
    {
        // Given a Manager and a target managed by a DIFFERENT manager
        var (dispatcher, context) = BuildHarness();
        var manager = await AddEmployeeAsync(context, role: "Manager");
        var otherManager = await AddEmployeeAsync(context, role: "Manager");
        var foreignSubordinate = await AddEmployeeAsync(context, managerId: otherManager.Id);

        // When the Manager queries the foreign subordinate
        var result = await dispatcher.DispatchAsync(BuildInput(
            BuildVector(ConversationIntention.LeaveBalance,
                new Dictionary<string, string?> { ["employee_id"] = foreignSubordinate.Id.ToString() }),
            BuildIdentity(manager.Id, "Manager", RoleFlags.Manager)));

        // Then the dispatch is rejected with SCOPE_MISMATCH
        var rejection = result.Should().BeOfType<DispatchRejected>().Subject;
        rejection.Reason.DenialCode.Should().Be(DenialCode.SCOPE_MISMATCH);
    }

    // --------------------------------------------------------------------- //
    // Manager scope via leave_request_id (RequiresManagerScope tool)
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_Manager_When_Leave_Request_Owner_Is_Outside_Team()
    {
        // Given a Manager and a leave request owned by an employee outside the team
        var (dispatcher, context) = BuildHarness();
        var manager = await AddEmployeeAsync(context, role: "Manager");
        var foreignEmployee = await AddEmployeeAsync(context, managerId: null);
        var leave = await AddLeaveRequestAsync(context, foreignEmployee.Id);

        // When the Manager approves a foreign leave request
        var result = await dispatcher.DispatchAsync(BuildInput(
            BuildVector(ConversationIntention.LeaveApproval,
                new Dictionary<string, string?> { ["leave_request_id"] = leave.Id.ToString() }),
            BuildIdentity(manager.Id, "Manager", RoleFlags.Manager)));

        // Then the leave owner is outside the team → SCOPE_MISMATCH
        var rejection = result.Should().BeOfType<DispatchRejected>().Subject;
        rejection.Reason.DenialCode.Should().Be(DenialCode.SCOPE_MISMATCH);
    }

    [Fact]
    public async Task Should_Allow_Manager_When_Leave_Request_Owner_Is_In_Team()
    {
        // Given a Manager and a leave request owned by their direct subordinate
        var (dispatcher, context) = BuildHarness();
        var manager = await AddEmployeeAsync(context, role: "Manager");
        var subordinate = await AddEmployeeAsync(context, managerId: manager.Id);
        var leave = await AddLeaveRequestAsync(context, subordinate.Id);

        // When the Manager approves the subordinate's leave request
        var result = await dispatcher.DispatchAsync(BuildInput(
            BuildVector(ConversationIntention.LeaveApproval,
                new Dictionary<string, string?> { ["leave_request_id"] = leave.Id.ToString() }),
            BuildIdentity(manager.Id, "Manager", RoleFlags.Manager)));

        // Then the dispatch reaches the approval tool
        var dispatch = result.Should().BeOfType<DispatchToTool>().Subject;
        dispatch.Dispatch.ToolName.Should().Be("ApprouverDemandeCongesAsync");
    }

    // --------------------------------------------------------------------- //
    // Admin exemption — scope checks never apply to Admin
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Not_Apply_Manager_Scope_To_Admin()
    {
        // Given an Admin targeting an employee outside any team (no manager)
        var (dispatcher, context) = BuildHarness();
        var orphan = await AddEmployeeAsync(context, managerId: null);

        // When the Admin queries the orphan
        var result = await dispatcher.DispatchAsync(BuildInput(
            BuildVector(ConversationIntention.LeaveBalance,
                new Dictionary<string, string?> { ["employee_id"] = orphan.Id.ToString() }),
            BuildIdentity(Guid.NewGuid(), "Admin", RoleFlags.Admin)));

        // Then NO SCOPE_MISMATCH is produced — the dispatch reaches the tool
        var dispatch = result.Should().BeOfType<DispatchToTool>().Subject;
        dispatch.Dispatch.ToolName.Should().Be("ConsulterSoldeAsync");
    }

    // --------------------------------------------------------------------- //
    // P0-1 — validated PreFlight parameters beat the extracted-entity fallback
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Prefer_Validated_Parameters_Over_Extracted_Entities()
    {
        // Given a cognitive vector whose EXTRACTED amount is 9999…
        var (dispatcher, _) = BuildHarness();

        // …while the PreFlight VALIDATED parameters carry amount = "500"
        var input = BuildInput(
            BuildVector(ConversationIntention.SalaryAdvance,
                new Dictionary<string, string?> { ["amount"] = "9999" }),
            BuildIdentity(Guid.NewGuid(), "Collaborator", RoleFlags.Collaborator),
            validated: new { amount = "500" });

        // When the dispatcher resolves the effective parameters
        var result = await dispatcher.DispatchAsync(input);

        // Then the VALIDATED parameters win — never the extracted fallback
        var dispatch = result.Should().BeOfType<DispatchToTool>().Subject;
        dispatch.Dispatch.Parameters.Should().ContainKey("amount");
        dispatch.Dispatch.Parameters["amount"]!.ToString().Should().Be("500");
        dispatch.Dispatch.Parameters["amount"]!.ToString().Should().NotBe("9999");
    }
}
