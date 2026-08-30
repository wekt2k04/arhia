using Arhia.Domain;
using Arhia.Domain.Entities;
using Arhia.Domain.ValueObjects;
using Arhia.Infrastructure.Persistence;
using Arhia.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Arhia.Tests.Persistence;

public class EmployeeRepositoryTests
{
    private static DbContextOptions<ArhiaDbContext> CreerOptions() =>
        new DbContextOptionsBuilder<ArhiaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task AddThenGetByEmployeeNumber_RebuildsTheValueObject()
    {
        var options = CreerOptions();
        var employeeNumber = new EmployeeNumber("MAT-042");
        var employee = new Employee(Guid.NewGuid(), employeeNumber, "Dupont", "Jean", "Développeur", Guid.NewGuid(), ContractType.CDI, new DateTime(2026, 1, 15));

        await using (var dbEcriture = new ArhiaDbContext(options))
        {
            await new EmployeeRepository(dbEcriture).AddAsync(employee);
        }

        await using var dbLecture = new ArhiaDbContext(options);
        var recharge = await new EmployeeRepository(dbLecture).GetByEmployeeNumberAsync(employeeNumber);

        recharge.Should().NotBeNull();
        recharge!.EmployeeNumber.Should().Be(employeeNumber);
        recharge.EmployeeNumber.Value.Should().Be("MAT-042");
        recharge.ContractType.Should().Be(ContractType.CDI);
    }

    [Fact]
    public async Task RecordDeparture_ThenReload_PersistsTheDepartureDate()
    {
        var options = CreerOptions();
        var employee = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT-099"), "Martin", "Marie", "RH", Guid.NewGuid(), ContractType.CDI, new DateTime(2020, 1, 1));
        employee.RecordDeparture(new DateTime(2026, 8, 15));

        await using (var dbEcriture = new ArhiaDbContext(options))
        {
            await new EmployeeRepository(dbEcriture).AddAsync(employee);
        }

        await using var dbLecture = new ArhiaDbContext(options);
        var recharge = await new EmployeeRepository(dbLecture).GetByIdAsync(employee.Id);

        recharge!.DepartureDate.Should().Be(new DateTime(2026, 8, 15));
    }

    [Fact]
    public async Task ListAllAsync_ReturnsEmployeesAcrossDepartments()
    {
        var options = CreerOptions();
        var employeeA = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT-A"), "A", "A", "Dev", Guid.NewGuid(), ContractType.CDI, new DateTime(2026, 8, 1));
        var employeeB = new Employee(Guid.NewGuid(), new EmployeeNumber("MAT-B"), "B", "B", "Dev", Guid.NewGuid(), ContractType.CDI, new DateTime(2026, 8, 1));

        await using (var db = new ArhiaDbContext(options))
        {
            var repo = new EmployeeRepository(db);
            await repo.AddAsync(employeeA);
            await repo.AddAsync(employeeB);
        }

        await using var dbLecture = new ArhiaDbContext(options);
        var resultat = await new EmployeeRepository(dbLecture).ListAllAsync();

        resultat.Should().HaveCount(2);
    }
}
