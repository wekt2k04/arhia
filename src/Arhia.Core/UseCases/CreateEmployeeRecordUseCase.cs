using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Core.Security;
using Arhia.Domain;
using Arhia.Domain.Entities;
using Arhia.Domain.ValueObjects;

namespace Arhia.Core.UseCases;

public sealed class CreateEmployeeRecordUseCase
{
    private readonly IEmployeeRepository _employees;

    public CreateEmployeeRecordUseCase(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<Employee> ExecuteAsync(
        UserAccount actor,
        EmployeeNumber employeeNumber,
        string lastName,
        string firstName,
        string jobTitle,
        Guid departmentId,
        ContractType contractType,
        DateTime startDate,
        CancellationToken ct = default)
    {
        if (!RbacMatrix.IsAuthorized(actor.Role, ResourceAction.EmployeeCreate))
            throw new AccessDeniedException("Seul un RH peut créer une fiche collaborateur.");

        if (!DepartmentScopeGuard.CanAccessDepartment(actor, departmentId))
            throw new AccessDeniedException("Un RH ne peut créer un collaborateur que dans son propre pôle.");

        var existing = await _employees.GetByEmployeeNumberAsync(employeeNumber, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Le matricule {employeeNumber} est déjà utilisé.");

        var employee = new Employee(
            Guid.NewGuid(), employeeNumber, lastName, firstName, jobTitle, departmentId, contractType, startDate);

        await _employees.AddAsync(employee, ct);
        return employee;
    }
}
