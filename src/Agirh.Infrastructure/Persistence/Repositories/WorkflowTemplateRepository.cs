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

    public async Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.WorkflowTemplates
            .Include(t => t.Sections)
            .ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<WorkflowTemplate?> GetLastApprovedAsync(WorkflowType type, CancellationToken ct = default) =>
        await _db.WorkflowTemplates
            .Include(t => t.Sections)
            .ThenInclude(s => s.Items)
            .Where(t => t.Type == type && t.Status == TemplateStatus.Approved)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<WorkflowTemplate>> ListByStatusAsync(TemplateStatus status, CancellationToken ct = default) =>
        await _db.WorkflowTemplates
            .Where(t => t.Status == status)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(WorkflowTemplate template, CancellationToken ct = default)
    {
        await _db.WorkflowTemplates.AddAsync(template, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WorkflowTemplate template, CancellationToken ct = default)
    {
        _db.WorkflowTemplates.Update(template);
        await _db.SaveChangesAsync(ct);
    }
}
