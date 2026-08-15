using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Infrastructure.Persistence.Repositories;

public class CompteUtilisateurRepository : ICompteUtilisateurRepository
{
    private readonly AgirhDbContext _db;

    public CompteUtilisateurRepository(AgirhDbContext db)
    {
        _db = db;
    }

    public async Task<CompteUtilisateur?> ObtenirParIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.ComptesUtilisateurs.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<CompteUtilisateur?> ObtenirParEmailAsync(string email, CancellationToken ct = default)
    {
        var emailNormalise = email.Trim().ToLowerInvariant();
        return await _db.ComptesUtilisateurs.FirstOrDefaultAsync(c => c.Email == emailNormalise, ct);
    }

    public async Task AjouterAsync(CompteUtilisateur compte, CancellationToken ct = default)
    {
        await _db.ComptesUtilisateurs.AddAsync(compte, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MettreAJourAsync(CompteUtilisateur compte, CancellationToken ct = default)
    {
        _db.ComptesUtilisateurs.Update(compte);
        await _db.SaveChangesAsync(ct);
    }
}
