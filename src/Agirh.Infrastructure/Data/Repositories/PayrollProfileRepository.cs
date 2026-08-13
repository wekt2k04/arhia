using Microsoft.EntityFrameworkCore;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Agirh.Infrastructure.Data;

namespace Agirh.Infrastructure.Repositories;

public class PayrollProfileRepository : IPayrollProfileRepository
{
    private readonly AppDbContext _context;
    public PayrollProfileRepository(AppDbContext context) => _context = context;

    public async Task<PayrollProfile?> GetByEmployeeIdAsync(Guid employeeId) =>
        await _context.PayrollProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.EmployeeId == employeeId);

    public async Task AddAsync(PayrollProfile profile)
    {
        profile.Id = Guid.NewGuid();
        await _context.PayrollProfiles.AddAsync(profile);
    }
}
