using System.Text.Json;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Agirh.Infrastructure.MAF;
using FluentAssertions;
using Xunit;

namespace Agirh.Tests.Unit;

/// <summary>
/// Behavior tests for <see cref="PoserDemandeCongesTool"/> — création d'une
/// demande de congés Pending (R5) : succès, IDOR collaborateur, date invalide.
/// </summary>
public class LeaveFunctionsTests
{
    private sealed class FakeLeaveRepo : ILeaveRequestRepository
    {
        public List<LeaveRequest> Items { get; } = [];
        public int SaveCount { get; private set; }

        public Task<LeaveRequest?> GetByIdAsync(Guid id) =>
            Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

        public Task<IEnumerable<LeaveRequest>> GetByEmployeeIdAsync(Guid employeeId) =>
            Task.FromResult(Items.Where(i => i.EmployeeId == employeeId).AsEnumerable());

        public Task AddAsync(LeaveRequest request) { Items.Add(request); return Task.CompletedTask; }
        public Task UpdateAsync(LeaveRequest request) => Task.CompletedTask;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) { SaveCount++; return Task.FromResult(1); }
    }

    private sealed class FakeEmployeeRepo : IEmployeeRepository
    {
        private readonly Employee _requester;
        public FakeEmployeeRepo(Employee requester) => _requester = requester;

        public Task<Employee?> GetByIdAsync(Guid id) => Task.FromResult(id == _requester.Id ? _requester : null);
        public Task<Employee?> GetByEmailAsync(string email) => Task.FromResult<Employee?>(null);
        public Task<IEnumerable<Employee>> GetAllAsync(int page = 1, int pageSize = 50) =>
            Task.FromResult(Enumerable.Empty<Employee>());
        public Task AddAsync(Employee employee) => Task.CompletedTask;
        public Task UpdateAsync(Employee employee) => Task.CompletedTask;
        public Task DeleteAsync(Guid id) => Task.CompletedTask;
    }

    private static Employee Jean() => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Jean",
        LastName = "Dupont",
        Email = "jean.dupont@agirh.test",
        Role = "Collaborator",
        IsActive = true,
    };

    private static async Task<string> RunAsync(PoserDemandeCongesInput input, Employee requester)
    {
        var leaveRepo = new FakeLeaveRepo();
        var tool = new PoserDemandeCongesTool(leaveRepo, new FakeEmployeeRepo(requester));
        var result = await tool.ExecuteAsync(JsonSerializer.SerializeToElement(input), requester.Id.ToString(), CancellationToken.None);
        return result;
    }

    [Fact]
    public async Task Should_Create_Pending_Leave_For_Self()
    {
        // Given un collaborateur qui pose un congé (date + jours valides)
        var jean = Jean();
        var leaveRepo = new FakeLeaveRepo();
        var tool = new PoserDemandeCongesTool(leaveRepo, new FakeEmployeeRepo(jean));

        // When l'outil est exécuté
        var result = await tool.ExecuteAsync(
            JsonSerializer.SerializeToElement(new PoserDemandeCongesInput("15/08/2026", "2", null)),
            jean.Id.ToString(), CancellationToken.None);

        // Then une demande Pending de 2 jours est créée et commitée
        result.Should().Contain("a été enregistrée");
        result.Should().Contain("2 jour(s)");
        var leave = leaveRepo.Items.Should().ContainSingle().Which;
        leave.EmployeeId.Should().Be(jean.Id);
        leave.Status.Should().Be("Pending");
        leave.DaysRequested.Should().Be(2);
        leave.Type.Should().Be("Conges");
        leaveRepo.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_Reject_IDOR_When_Collaborator_Targets_Another_Employee()
    {
        // Given un collaborateur qui tente de poser un congé pour un autre employé
        var jean = Jean();
        var marieId = Guid.NewGuid();
        var leaveRepo = new FakeLeaveRepo();
        var tool = new PoserDemandeCongesTool(leaveRepo, new FakeEmployeeRepo(jean));

        // When l'outil est exécuté avec employeeId = Marie
        var result = await tool.ExecuteAsync(
            JsonSerializer.SerializeToElement(new PoserDemandeCongesInput("15/08/2026", "2", marieId.ToString())),
            jean.Id.ToString(), CancellationToken.None);

        // Then l'action est refusée et aucune demande n'est créée
        result.Should().Contain("Action non autorisée");
        leaveRepo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Reject_Invalid_Date()
    {
        // Given une date invalide (13e mois)
        var jean = Jean();

        // When l'outil est exécuté
        var result = await RunAsync(new PoserDemandeCongesInput("15/13/2026", "2", null), jean);

        // Then un message d'erreur clair est retourné (intercepté par l'orchestrateur)
        result.Should().Contain("date de début invalide");
    }

    [Fact]
    public async Task Should_Reject_Invalid_Days()
    {
        var jean = Jean();
        var result = await RunAsync(new PoserDemandeCongesInput("15/08/2026", "0", null), jean);
        result.Should().Contain("nombre de jours invalide");
    }
}
