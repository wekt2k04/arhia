using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Infrastructure.Persistence.Repositories;

public class WorkflowInstanceRepository : IWorkflowInstanceRepository
{
    private readonly AgirhDbContext _db;

    public WorkflowInstanceRepository(AgirhDbContext db)
    {
        _db = db;
    }

    public async Task<WorkflowInstance?> ObtenirParIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.WorkflowInstances.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<WorkflowInstance?> ObtenirParCollaborateurAsync(Guid collaborateurId, WorkflowType type, CancellationToken ct = default) =>
        await _db.WorkflowInstances
            .Include(i => i.Items)
            .Where(i => i.CollaborateurId == collaborateurId && i.Type == type)
            .OrderByDescending(i => i.DateCreation)
            .FirstOrDefaultAsync(ct);

    public async Task AjouterAsync(WorkflowInstance instance, CancellationToken ct = default)
    {
        await _db.WorkflowInstances.AddAsync(instance, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MettreAJourAsync(WorkflowInstance instance, CancellationToken ct = default)
    {
        _db.WorkflowInstances.Update(instance);
        await _db.SaveChangesAsync(ct);
    }
}
