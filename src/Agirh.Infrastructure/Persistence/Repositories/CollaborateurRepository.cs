using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Infrastructure.Persistence.Repositories;

public class CollaborateurRepository : ICollaborateurRepository
{
    private readonly AgirhDbContext _db;

    public CollaborateurRepository(AgirhDbContext db)
    {
        _db = db;
    }

    public async Task<Collaborateur?> ObtenirParIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Collaborateurs.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Collaborateur?> ObtenirParMatriculeAsync(Matricule matricule, CancellationToken ct = default) =>
        await _db.Collaborateurs.FirstOrDefaultAsync(c => c.Matricule == matricule, ct);

    public async Task<Collaborateur?> ObtenirParCompteUtilisateurIdAsync(Guid compteUtilisateurId, CancellationToken ct = default) =>
        await _db.Collaborateurs.FirstOrDefaultAsync(c => c.CompteUtilisateurId == compteUtilisateurId, ct);

    public async Task AjouterAsync(Collaborateur collaborateur, CancellationToken ct = default)
    {
        await _db.Collaborateurs.AddAsync(collaborateur, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MettreAJourAsync(Collaborateur collaborateur, CancellationToken ct = default)
    {
        _db.Collaborateurs.Update(collaborateur);
        await _db.SaveChangesAsync(ct);
    }
}
