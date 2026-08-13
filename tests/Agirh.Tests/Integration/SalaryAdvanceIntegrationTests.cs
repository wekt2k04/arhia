using System.Text.Json;
using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Data;
using Agirh.Infrastructure.MAF;
using Agirh.Infrastructure.Repositories;
using Agirh.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agirh.Tests.Integration;

/// <summary>
/// Integration tests for the <see cref="DemanderAvanceSalaireAsync"/> MAF tool.
///
/// Each test uses a FRESH InMemory database (unique name per test) so tests are
/// fully isolated and auto-cleaned by the provider. Real repositories and a real
/// <see cref="UnitOfWork"/> are wired against the context; the tool is exercised
/// through the public <see cref="IMafTool.ExecuteAsync"/> entry point exactly as
/// the MAF agent would call it.
/// </summary>
public class SalaryAdvanceIntegrationTests
{
    // --------------------------------------------------------------------- //
    // Helpers
    // --------------------------------------------------------------------- //

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IMafTool BuildTool(AppDbContext context)
    {
        var payrollRepo = new PayrollProfileRepository(context);
        var advanceRepo = new SalaryAdvanceRepository(context);
        var unitOfWork = new UnitOfWork(
            context,
            new EmployeeRepository(context),
            new LeaveRequestRepository(context),
            payrollRepo,
            advanceRepo,
            new AgentConversationRepository(context));

        return new DemanderAvanceSalaireAsync(payrollRepo, advanceRepo, unitOfWork);
    }

    private static async Task<Employee> AddEmployeeAsync(
        AppDbContext context,
        string firstName,
        string lastName,
        string role = "Collaborator")
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@agirh.test",
            Role = role,
            IsActive = true
        };

        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        return employee;
    }

    private static async Task<PayrollProfile> AddPayrollProfileAsync(
        AppDbContext context,
        Guid employeeId,
        decimal netSalary = 10000m,
        decimal maxAdvancePercentage = 0.50m)
    {
        var profile = new PayrollProfile
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            NetSalary = netSalary,
            MaxAdvancePercentage = maxAdvancePercentage,
            Iban = "FR7630006000011234567890189"
        };

        context.PayrollProfiles.Add(profile);
        await context.SaveChangesAsync();
        return profile;
    }

    private static async Task<SalaryAdvanceRequest> AddSalaryAdvanceAsync(
        AppDbContext context,
        Guid employeeId,
        string status,
        decimal amount)
    {
        var request = new SalaryAdvanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            AmountRequested = amount,
            Status = status,
            RequestDate = DateTime.UtcNow
        };

        context.SalaryAdvanceRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }

    private static JsonElement Parameters(string employeeId, decimal amount)
        => JsonSerializer.SerializeToElement(new { employeeId, amount });

    // --------------------------------------------------------------------- //
    // 1. Happy path: valid request is persisted as Pending
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Create_Pending_Advance_When_Valid()
    {
        // Arrange
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        await AddPayrollProfileAsync(context, employee.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var tool = BuildTool(context);

        // Act
        var result = await tool.ExecuteAsync(
            Parameters(employee.Id.ToString(), 3000m),
            requestingUserId: employee.Id.ToString(),
            CancellationToken.None);

        // Assert — EXACT message produced by the use case (Phase 3), DB persisted
        result.Should().StartWith("Votre demande d'avance sur salaire de 3000 € a été enregistrée avec succès et est en attente d'approbation par la RH.")
            .And.Contain("||WIDGET:SalaryAdvance:");

        var requests = context.SalaryAdvanceRequests.ToList();
        requests.Should().HaveCount(1);
        requests[0].Status.Should().Be("Pending");
        requests[0].AmountRequested.Should().Be(3000m);
    }

    // --------------------------------------------------------------------- //
    // 2. Cap enforcement: 6000 > 50% of 10000 (5000) => rejected, nothing saved
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_Advance_When_Amount_Exceeds_50_Percent()
    {
        // Arrange
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        await AddPayrollProfileAsync(context, employee.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var tool = BuildTool(context);

        // Act
        var result = await tool.ExecuteAsync(
            Parameters(employee.Id.ToString(), 6000m),
            requestingUserId: employee.Id.ToString(),
            CancellationToken.None);

        // Assert — EXACT cap-refusal message, nothing saved
        result.Should().Be("Demande refusée : le montant demandé dépasse votre plafond autorisé pour une avance sur salaire.");
        context.SalaryAdvanceRequests.ToList().Should().BeEmpty();
    }

    // --------------------------------------------------------------------- //
    // 3. One pending request at a time => duplicate rejected, no new row
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_Advance_When_Existing_Pending_Request()
    {
        // Arrange
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        await AddPayrollProfileAsync(context, employee.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var existing = await AddSalaryAdvanceAsync(context, employee.Id, status: "Pending", amount: 2000m);
        var tool = BuildTool(context);

        // Act
        var result = await tool.ExecuteAsync(
            Parameters(employee.Id.ToString(), 1000m),
            requestingUserId: employee.Id.ToString(),
            CancellationToken.None);

        // Assert — EXACT duplicate message, only the original row remains
        result.Should().Be("Vous avez déjà une demande d'avance sur salaire en attente. Elle doit être traitée avant d'en soumettre une nouvelle.");

        var requests = context.SalaryAdvanceRequests.ToList();
        requests.Should().HaveCount(1);
        requests.Single().Id.Should().Be(existing.Id);
    }

    // --------------------------------------------------------------------- //
    // 4. IDOR guard: Collaborator must not request an advance for someone else
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Block_Idor_Attempt_By_Collaborator()
    {
        // Arrange
        var context = CreateContext();
        var jean = await AddEmployeeAsync(context, "Jean", "Dupont", role: "Collaborator");
        var marie = await AddEmployeeAsync(context, "Marie", "Curie", role: "Collaborator");
        await AddPayrollProfileAsync(context, marie.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var tool = BuildTool(context);

        // Act
        var result = await tool.ExecuteAsync(
            Parameters(marie.Id.ToString(), 1000m),
            requestingUserId: jean.Id.ToString(),
            CancellationToken.None);

        // Assert
        result.Should().Be("Action non autorisée. Vous ne pouvez demander une avance que pour vous-même.");
        context.SalaryAdvanceRequests.ToList().Should().BeEmpty();
    }

    // --------------------------------------------------------------------- //
    // 5. Security fail-closed: requestingUserId missing / unparseable
    // --------------------------------------------------------------------- //

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Should_Deny_When_RequestingUserId_Missing_Or_Invalid(string? requestingUserId)
    {
        // Arrange
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        await AddPayrollProfileAsync(context, employee.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var tool = BuildTool(context);

        // Act
        var result = await tool.ExecuteAsync(
            Parameters(employee.Id.ToString(), 1000m),
            requestingUserId: requestingUserId,
            CancellationToken.None);

        // Assert
        result.Should().Be("Erreur : impossible de résoudre l'identité de l'appelant. Action refusée.");
        context.SalaryAdvanceRequests.ToList().Should().BeEmpty();
    }

    // --------------------------------------------------------------------- //
    // 6. Security fail-closed: requester account does not exist
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Deny_When_Requester_Account_Not_Found()
    {
        // Arrange
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        await AddPayrollProfileAsync(context, employee.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var tool = BuildTool(context);

        // Act — a syntactically valid GUID that is NOT in the database
        var result = await tool.ExecuteAsync(
            Parameters(employee.Id.ToString(), 1000m),
            requestingUserId: Guid.NewGuid().ToString(),
            CancellationToken.None);

        // Assert
        result.Should().Be("Erreur : compte appelant introuvable. Action refusée.");
        context.SalaryAdvanceRequests.ToList().Should().BeEmpty();
    }

    // --------------------------------------------------------------------- //
    // 7. Failure (Null/Missing): no payroll profile => clean rejection
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_When_No_Payroll_Profile_Exists()
    {
        // Arrange — employee exists, but no PayrollProfile has been created
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        var tool = BuildTool(context);

        // Act
        var result = await tool.ExecuteAsync(
            Parameters(employee.Id.ToString(), 1000m),
            requestingUserId: employee.Id.ToString(),
            CancellationToken.None);

        // Assert
        result.Should().Be("Erreur : aucun profil paie n'existe pour ce collaborateur. Veuillez contacter la RH.");
        context.SalaryAdvanceRequests.ToList().Should().BeEmpty();
    }

    // --------------------------------------------------------------------- //
    // 8. Failure (Null/Missing): absent employeeId falls back to JWT identity
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Accept_Advance_When_EmployeeId_Missing_Falls_Back_To_Requester()
    {
        // Arrange
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        await AddPayrollProfileAsync(context, employee.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var tool = BuildTool(context);

        // Act — employeeId omitted; the tool must target the requester's own JWT identity
        var result = await tool.ExecuteAsync(
            JsonSerializer.SerializeToElement(new { amount = 1000m }),
            requestingUserId: employee.Id.ToString(),
            CancellationToken.None);

        // Assert — EXACT success message; the fallback targets the requester's own JWT identity
        result.Should().StartWith("Votre demande d'avance sur salaire de 1000 € a été enregistrée avec succès et est en attente d'approbation par la RH.")
            .And.Contain("||WIDGET:SalaryAdvance:");

        var requests = context.SalaryAdvanceRequests.ToList();
        requests.Should().HaveCount(1);
        requests[0].EmployeeId.Should().Be(employee.Id);
        requests[0].Status.Should().Be("Pending");
    }

    // --------------------------------------------------------------------- //
    // 9. Boundary: exactly 50% of net salary is accepted (guard is strictly >)
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Accept_Advance_When_Amount_Equals_Exact_Cap()
    {
        // Arrange — 5000 is exactly 50% of a 10000 net salary
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        await AddPayrollProfileAsync(context, employee.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var tool = BuildTool(context);

        // Act
        var result = await tool.ExecuteAsync(
            Parameters(employee.Id.ToString(), 5000m),
            requestingUserId: employee.Id.ToString(),
            CancellationToken.None);

        // Assert — EXACT success message; the boundary (== cap) is accepted
        result.Should().StartWith("Votre demande d'avance sur salaire de 5000 € a été enregistrée avec succès et est en attente d'approbation par la RH.")
            .And.Contain("||WIDGET:SalaryAdvance:");

        var requests = context.SalaryAdvanceRequests.ToList();
        requests.Should().HaveCount(1);
        requests[0].AmountRequested.Should().Be(5000m);
        requests[0].Status.Should().Be("Pending");
    }

    // --------------------------------------------------------------------- //
    // 10. Boundary/Failure: non-positive amount is rejected, nothing saved
    // --------------------------------------------------------------------- //

    [Theory]
    [InlineData(0d)]
    [InlineData(-100d)]
    public async Task Should_Reject_When_Amount_Not_Positive(double amount)
    {
        // Arrange
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        await AddPayrollProfileAsync(context, employee.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var tool = BuildTool(context);

        // Act
        var result = await tool.ExecuteAsync(
            Parameters(employee.Id.ToString(), (decimal)amount),
            requestingUserId: employee.Id.ToString(),
            CancellationToken.None);

        // Assert
        result.Should().Be("Erreur : le montant demandé doit être supérieur à zéro.");
        context.SalaryAdvanceRequests.ToList().Should().BeEmpty();
    }

    // --------------------------------------------------------------------- //
    // 11. PreFlight (Null/Missing): amount is mandatory, employee_id optional
    // --------------------------------------------------------------------- //

    [Fact]
    public void PreFlight_Should_Require_Amount_And_Treat_EmployeeId_As_Optional()
    {
        var validator = new PreFlightValidator();

        // Missing amount => invalid, even when employee_id is provided
        var missingAmount = validator.ValidateParameters(
            "DemanderAvanceSalaireAsync",
            new Dictionary<string, string?> { ["employee_id"] = Guid.NewGuid().ToString() });

        missingAmount.IsValid.Should().BeFalse();
        missingAmount.ErrorMessage.Should().Be("Paramètres obligatoires manquants : montant souhaité de l'avance.");

        // Amount present, employee_id absent => valid (the tool will fall back to the JWT identity)
        var valid = validator.ValidateParameters(
            "DemanderAvanceSalaireAsync",
            new Dictionary<string, string?> { ["amount"] = "1000" });

        valid.IsValid.Should().BeTrue();
        valid.ErrorMessage.Should().BeNull();
        valid.ValidatedJson.GetProperty("amount").GetString().Should().Be("1000");
        valid.ValidatedJson.TryGetProperty("employeeId", out _).Should().BeFalse();
    }

    // --------------------------------------------------------------------- //
    // 12. Boundary: 5000.01 is just ONE cent above the 50% cap of 10000 => rejected
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Reject_Advance_When_Amount_Just_Above_Cap()
    {
        // Arrange — 5000.01 > 5000 (50% of a 10000 net salary) by the smallest unit
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        await AddPayrollProfileAsync(context, employee.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var tool = BuildTool(context);

        // Act
        var result = await tool.ExecuteAsync(
            Parameters(employee.Id.ToString(), 5000.01m),
            requestingUserId: employee.Id.ToString(),
            CancellationToken.None);

        // Assert — EXACT cap-refusal message, nothing saved
        result.Should().Be("Demande refusée : le montant demandé dépasse votre plafond autorisé pour une avance sur salaire.");
        context.SalaryAdvanceRequests.ToList().Should().BeEmpty();
    }

    // --------------------------------------------------------------------- //
    // 13. Boundary/Security: employeeId that is NOT a GUID falls back to the
    //     requester's JWT identity (symmetric to the missing-employeeId case)
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Should_Accept_Advance_When_EmployeeId_Is_Not_A_Guid_Falls_Back_To_Requester()
    {
        // Arrange
        var context = CreateContext();
        var employee = await AddEmployeeAsync(context, "Jean", "Dupont");
        await AddPayrollProfileAsync(context, employee.Id, netSalary: 10000m, maxAdvancePercentage: 0.50m);
        var tool = BuildTool(context);

        // Act — employeeId = "abc" (unparseable); the tool must target the requester's own identity
        var result = await tool.ExecuteAsync(
            Parameters("abc", 1000m),
            requestingUserId: employee.Id.ToString(),
            CancellationToken.None);

        // Assert — EXACT success message; the advance is created FOR THE REQUESTER
        result.Should().StartWith("Votre demande d'avance sur salaire de 1000 € a été enregistrée avec succès et est en attente d'approbation par la RH.")
            .And.Contain("||WIDGET:SalaryAdvance:");

        var requests = context.SalaryAdvanceRequests.ToList();
        requests.Should().HaveCount(1);
        requests[0].EmployeeId.Should().Be(employee.Id);
        requests[0].Status.Should().Be("Pending");
        requests[0].AmountRequested.Should().Be(1000m);
    }
}
