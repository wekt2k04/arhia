using Agirh.Domain.Entities;

namespace Agirh.Domain.Interfaces;

public interface ISalaryAdvanceRepository
{
    Task<SalaryAdvanceRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<SalaryAdvanceRequest>> GetByEmployeeIdAsync(Guid employeeId);
    Task<bool> HasPendingRequestAsync(Guid employeeId);
    Task AddAsync(SalaryAdvanceRequest request);
}
