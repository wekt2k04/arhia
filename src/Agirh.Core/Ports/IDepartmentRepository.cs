using System.Threading;
using System.Threading.Tasks;
using Agirh.Domain.Entities;

namespace Agirh.Core.Ports;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Department department, CancellationToken ct = default);
}
