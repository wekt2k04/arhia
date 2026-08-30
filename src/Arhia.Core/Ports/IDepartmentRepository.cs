using System.Threading;
using System.Threading.Tasks;
using Arhia.Domain.Entities;

namespace Arhia.Core.Ports;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Department>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(Department department, CancellationToken ct = default);
}
