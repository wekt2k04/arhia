using Agirh.Domain.Entities;

namespace Agirh.Domain.Interfaces;

public interface ILeaveRequestRepository
{
    Task<LeaveRequest?> GetByIdAsync(Guid id);
    Task<IEnumerable<LeaveRequest>> GetByEmployeeIdAsync(Guid employeeId);
    Task AddAsync(LeaveRequest request);
    Task UpdateAsync(LeaveRequest request);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
