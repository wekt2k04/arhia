using Microsoft.EntityFrameworkCore;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Agirh.Infrastructure.Data;

namespace Agirh.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _context;
    public EmployeeRepository(AppDbContext context) => _context = context;

    public async Task<Employee?> GetByIdAsync(Guid id) =>
        await _context.Employees.AsNoTracking().Include(e => e.Manager).FirstOrDefaultAsync(e => e.Id == id);

    public async Task<Employee?> GetByEmailAsync(string email) =>
        await _context.Employees.AsNoTracking().Include(e => e.Manager).FirstOrDefaultAsync(e => e.Email == email);

    public async Task<IEnumerable<Employee>> GetAllAsync(int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;

        return await _context.Employees
            .AsNoTracking()
            .OrderBy(e => e.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task AddAsync(Employee employee)
    {
        employee.Id = Guid.NewGuid();
        await _context.Employees.AddAsync(employee);
    }

    public Task UpdateAsync(Employee employee)
    {
        _context.Employees.Update(employee);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee != null)
            _context.Employees.Remove(employee);
    }
}
