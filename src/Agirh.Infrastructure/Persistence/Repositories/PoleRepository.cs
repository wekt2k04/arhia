using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Infrastructure.Persistence.Repositories;

public class PoleRepository : IPoleRepository
{
    private readonly AgirhDbContext _db;

    public PoleRepository(AgirhDbContext db)
    {
        _db = db;
    }

    public async Task<Pole?> ObtenirParIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Poles.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AjouterAsync(Pole pole, CancellationToken ct = default)
    {
        await _db.Poles.AddAsync(pole, ct);
        await _db.SaveChangesAsync(ct);
    }
}
