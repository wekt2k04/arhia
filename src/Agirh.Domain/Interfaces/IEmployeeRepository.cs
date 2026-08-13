using Agirh.Domain.Entities;

namespace Agirh.Domain.Interfaces;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id);
    Task<Employee?> GetByEmailAsync(string email);
    Task<IEnumerable<Employee>> GetAllAsync(int page = 1, int pageSize = 50);
    Task AddAsync(Employee employee);
    Task UpdateAsync(Employee employee);
    Task DeleteAsync(Guid id);
}
