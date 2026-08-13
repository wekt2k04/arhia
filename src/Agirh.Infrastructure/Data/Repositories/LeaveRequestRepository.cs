using Microsoft.EntityFrameworkCore;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Agirh.Infrastructure.Data;

namespace Agirh.Infrastructure.Repositories;

public class LeaveRequestRepository : ILeaveRequestRepository
{
    private readonly AppDbContext _context;
    public LeaveRequestRepository(AppDbContext context) => _context = context;

    public async Task<LeaveRequest?> GetByIdAsync(Guid id) =>
        await _context.LeaveRequests.AsNoTracking().Include(l => l.Employee).Include(l => l.ApprovedBy).FirstOrDefaultAsync(l => l.Id == id);

    public async Task<IEnumerable<LeaveRequest>> GetByEmployeeIdAsync(Guid employeeId) =>
        await _context.LeaveRequests.AsNoTracking().Include(l => l.Employee).Include(l => l.ApprovedBy).Where(l => l.EmployeeId == employeeId).ToListAsync();

    public async Task AddAsync(LeaveRequest request)
    {
        request.Id = Guid.NewGuid();
        request.CreatedAt = DateTime.UtcNow;
        await _context.LeaveRequests.AddAsync(request);
    }

    public Task UpdateAsync(LeaveRequest request)
    {
        _context.LeaveRequests.Update(request);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
