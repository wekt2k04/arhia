using Microsoft.EntityFrameworkCore;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Agirh.Infrastructure.Data;

namespace Agirh.Infrastructure.Repositories;

public class SalaryAdvanceRepository : ISalaryAdvanceRepository
{
    private readonly AppDbContext _context;
    public SalaryAdvanceRepository(AppDbContext context) => _context = context;

    public async Task<SalaryAdvanceRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.SalaryAdvanceRequests.FindAsync(new object[] { id }, ct);

    public async Task<IEnumerable<SalaryAdvanceRequest>> GetByEmployeeIdAsync(Guid employeeId) =>
        await _context.SalaryAdvanceRequests.AsNoTracking()
            .Include(s => s.Employee)
            .Where(s => s.EmployeeId == employeeId)
            .ToListAsync();

    public async Task<bool> HasPendingRequestAsync(Guid employeeId) =>
        await _context.SalaryAdvanceRequests.AnyAsync(s => s.EmployeeId == employeeId && s.Status == "Pending");

    public async Task AddAsync(SalaryAdvanceRequest request)
    {
        await _context.SalaryAdvanceRequests.AddAsync(request);
    }
}
