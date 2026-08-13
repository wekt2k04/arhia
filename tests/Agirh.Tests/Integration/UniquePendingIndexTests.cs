using Agirh.Domain.Entities;
using Agirh.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agirh.Tests.Integration;

/// <summary>
/// Tests for the TOCTOU hardening: a UNIQUE FILTERED index on
/// SalaryAdvanceRequests(EmployeeId) WHERE Status = 'Pending' (correctif 4.4).
///
/// (a) METADATA — deterministic, EF InMemory: the model exposes the unique
///     filtered index even though the InMemory provider never applies it.
/// (b) CONCURRENCE — guarded REAL SQL Server test: only runs when the dev
///     container (agirh-sql:1433, fallback localhost:1433, sa/YourStrong!Passw0rd)
///     is reachable; otherwise the test returns silently so NO external
///     dependency can break the suite. When reachable it proves the database
///     itself rejects the second concurrent "Pending" insert for one employee.
/// </summary>
public class UniquePendingIndexTests
{
    private const string DevPassword = "YourStrong!Passw0rd";

    // --------------------------------------------------------------------- //
    // (a) Metadata — the model must carry the unique filtered index
    // --------------------------------------------------------------------- //

    [Fact]
    public void Model_Should_Define_Unique_Filtered_Index_On_EmployeeId_For_Pending_Status()
    {
        // Given an InMemory context built from the REAL AppDbContext model
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options);

        // When the SalaryAdvanceRequest entity type is inspected
        var entityType = context.Model.FindEntityType(typeof(SalaryAdvanceRequest));
        entityType.Should().NotBeNull();

        // Then it exposes a UNIQUE index on EmployeeId whose filter targets "Pending"
        var indexes = entityType!.GetIndexes().ToList();
        var uniqueIndex = indexes.Should().ContainSingle(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(SalaryAdvanceRequest.EmployeeId))).Subject;
        uniqueIndex.Properties.Select(p => p.Name).Should().Contain(nameof(SalaryAdvanceRequest.EmployeeId));
        uniqueIndex.GetFilter().Should().Contain("Pending");
    }

    // --------------------------------------------------------------------- //
    // (b) Real concurrency — guarded: silently skips when SQL is unreachable
    // --------------------------------------------------------------------- //

    [Fact]
    public async Task Concurrent_Pending_Advances_For_Same_Employee_Should_Be_Blocked_By_Database_Index()
    {
        // Guard (spec 4.4b) : le test SQL ne tourne QUE si le container de dev
        // agirh-sql:1433 est joignable. Sinon return silencieux — aucune dépendance
        // externe ne peut casser la suite. (Ne pas étendre à d'autres hôtes : la
        // chaîne de migrations existante contient un ALTER vector(768) qui échoue
        // sur certaines builds SQL 2025 hors container — hors périmètre Phase 4.)
        var host = await FindReachableSqlHostAsync();
        if (host is null)
            return; // SQL container not reachable — test skipped silently (green)

        var dbName = $"AgirhTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($"Server={host};Database={dbName};User Id=sa;Password={DevPassword};TrustServerCertificate=True;Connect Timeout=15")
            .Options;

        try
        {
            // When the schema (including the new filtered index) is migrated…
            await using (var context = new AppDbContext(options))
            {
                await context.Database.MigrateAsync();

                // …an employee exists (FK target)…
                var employee = new Employee
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Concurrence",
                    LastName = "Test",
                    Email = $"concurrence-{Guid.NewGuid():N}@agirh.test",
                    Role = "Collaborator",
                    IsActive = true,
                };
                context.Employees.Add(employee);
                await context.SaveChangesAsync();

                // …and two Pending advances for the SAME employee are inserted at once
                var outcomes = await Task.WhenAll(
                    InsertPendingAdvanceAsync(options, employee.Id, 1000m),
                    InsertPendingAdvanceAsync(options, employee.Id, 2000m));

                // Then exactly ONE insert succeeds…
                outcomes.Count(o => o.Succeeded).Should().Be(1);

                // …and the other is rejected by the unique filtered index
                var failure = outcomes.Single(o => !o.Succeeded);
                failure.Exception.Should().BeAssignableTo<DbUpdateException>();

                // Only one Pending row remains for that employee
                context.SalaryAdvanceRequests.Count(r => r.EmployeeId == employee.Id && r.Status == "Pending").Should().Be(1);
            }
        }
        finally
        {
            await DropDatabaseAsync(options, dbName);
        }
    }

    // --------------------------------------------------------------------- //
    // Helpers
    // --------------------------------------------------------------------- //

    private sealed record InsertOutcome(bool Succeeded, System.Exception? Exception);

    private static async Task<InsertOutcome> InsertPendingAdvanceAsync(
        DbContextOptions<AppDbContext> options,
        Guid employeeId,
        decimal amount)
    {
        try
        {
            await using var context = new AppDbContext(options);
            await context.SalaryAdvanceRequests.AddAsync(new SalaryAdvanceRequest
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                AmountRequested = amount,
                Status = "Pending",
                RequestDate = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
            return new InsertOutcome(true, null);
        }
        catch (System.Exception ex)
        {
            return new InsertOutcome(false, ex);
        }
    }

    private static async Task<string?> FindReachableSqlHostAsync()
    {
        // Spec 4.4b : uniquement le container de dev agirh-sql:1433.
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer($"Server=agirh-sql,1433;Database=master;User Id=sa;Password={DevPassword};TrustServerCertificate=True;Connect Timeout=3")
                .Options;
            await using var context = new AppDbContext(options);
            if (await context.Database.CanConnectAsync())
                return "agirh-sql,1433";
        }
        catch
        {
            // container not reachable
        }

        return null;
    }

    private static async Task DropDatabaseAsync(DbContextOptions<AppDbContext> options, string dbName)
    {
        try
        {
            // The database name is a test-generated GUID (AgirhTests_<n>) — no injection surface.
            // T-SQL ne permet PAS de paramétrer le nom de base dans DROP DATABASE : d'où la suppression ciblée du warning.
#pragma warning disable EF1002
            await using var context = new AppDbContext(options);
            await context.Database.ExecuteSqlRawAsync(
                $"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}];");
#pragma warning restore EF1002
        }
        catch
        {
            // best-effort cleanup — never fail the test on teardown
        }
    }
}
