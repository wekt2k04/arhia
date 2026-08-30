using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Arhia.Infrastructure.Persistence.Repositories;

public class UserAccountRepository : IUserAccountRepository
{
    private readonly ArhiaDbContext _db;

    public UserAccountRepository(ArhiaDbContext db)
    {
        _db = db;
    }

    public async Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.UserAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _db.UserAccounts.FirstOrDefaultAsync(a => a.Email == normalizedEmail, ct);
    }

    public async Task AddAsync(UserAccount account, CancellationToken ct = default)
    {
        await _db.UserAccounts.AddAsync(account, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserAccount account, CancellationToken ct = default)
    {
        _db.UserAccounts.Update(account);
        await _db.SaveChangesAsync(ct);
    }
}
