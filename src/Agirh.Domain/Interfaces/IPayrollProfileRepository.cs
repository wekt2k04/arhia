using Agirh.Domain.Entities;

namespace Agirh.Domain.Interfaces;

public interface IPayrollProfileRepository
{
    Task<PayrollProfile?> GetByEmployeeIdAsync(Guid employeeId);
    Task AddAsync(PayrollProfile profile);
}
