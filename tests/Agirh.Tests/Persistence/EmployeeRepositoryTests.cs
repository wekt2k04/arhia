using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using Agirh.Infrastructure.Persistence;
using Agirh.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Tests.Persistence;

public class EmployeeRepositoryTests
{
    private static DbContextOptions<AgirhDbContext> CreerOptions() =>
        new DbContextOptionsBuilder<AgirhDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task AddThenGetByEmployeeNumber_RebuildsTheValueObject()
    {
        var options = CreerOptions();
        var employeeNumber = new EmployeeNumber("MAT-042");
        var employee = new Employee(Guid.NewGuid(), employeeNumber, "Dupont", "Jean", "Développeur", Guid.NewGuid(), ContractType.CDI, new DateTime(2026, 1, 15));

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new EmployeeRepository(dbEcriture).AddAsync(employee);
        }

        await using var dbLecture = new AgirhDbContext(options);
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

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new EmployeeRepository(dbEcriture).AddAsync(employee);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new EmployeeRepository(dbLecture).GetByIdAsync(employee.Id);

        recharge!.DepartureDate.Should().Be(new DateTime(2026, 8, 15));
    }
}
