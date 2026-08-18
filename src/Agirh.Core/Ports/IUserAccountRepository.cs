using System.Threading;
using System.Threading.Tasks;
using Agirh.Domain.Entities;

namespace Agirh.Core.Ports;

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(UserAccount account, CancellationToken ct = default);
    Task UpdateAsync(UserAccount account, CancellationToken ct = default);
}
