using System.Threading;
using System.Threading.Tasks;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;

namespace Agirh.Core.Ports;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Employee?> GetByEmployeeNumberAsync(EmployeeNumber employeeNumber, CancellationToken ct = default);
    Task<Employee?> GetByUserAccountIdAsync(Guid userAccountId, CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> ListByDepartmentAsync(Guid departmentId, CancellationToken ct = default);
    Task AddAsync(Employee employee, CancellationToken ct = default);
    Task UpdateAsync(Employee employee, CancellationToken ct = default);
}
