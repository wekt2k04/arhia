using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Domain;
using Agirh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Infrastructure.Persistence.Repositories;

public class WorkflowTemplateRepository : IWorkflowTemplateRepository
{
    private readonly AgirhDbContext _db;

    public WorkflowTemplateRepository(AgirhDbContext db)
    {
        _db = db;
    }

    public async Task<WorkflowTemplate?> ObtenirParIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.WorkflowTemplates
            .Include(t => t.Sections)
            .ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<WorkflowTemplate?> ObtenirDernierApprouveAsync(WorkflowType type, CancellationToken ct = default) =>
        await _db.WorkflowTemplates
            .Include(t => t.Sections)
            .ThenInclude(s => s.Items)
            .Where(t => t.Type == type && t.Statut == TemplateStatut.Approuve)
            .OrderByDescending(t => t.DateCreation)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<WorkflowTemplate>> ListerParStatutAsync(TemplateStatut statut, CancellationToken ct = default) =>
        await _db.WorkflowTemplates
            .Where(t => t.Statut == statut)
            .OrderBy(t => t.DateCreation)
            .ToListAsync(ct);

    public async Task AjouterAsync(WorkflowTemplate template, CancellationToken ct = default)
    {
        await _db.WorkflowTemplates.AddAsync(template, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MettreAJourAsync(WorkflowTemplate template, CancellationToken ct = default)
    {
        _db.WorkflowTemplates.Update(template);
        await _db.SaveChangesAsync(ct);
    }
}
