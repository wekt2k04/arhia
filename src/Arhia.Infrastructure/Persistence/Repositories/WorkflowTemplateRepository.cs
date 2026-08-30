using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Domain;
using Arhia.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Arhia.Infrastructure.Persistence.Repositories;

public class WorkflowTemplateRepository : IWorkflowTemplateRepository
{
    private readonly ArhiaDbContext _db;

    public WorkflowTemplateRepository(ArhiaDbContext db)
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
