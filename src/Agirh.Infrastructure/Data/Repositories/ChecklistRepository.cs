using Microsoft.EntityFrameworkCore;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;
using Agirh.Infrastructure.Data;

namespace Agirh.Infrastructure.Repositories;

public class ChecklistRepository : IChecklistRepository
{
    private readonly AppDbContext _context;
    public ChecklistRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<ChecklistItem>> GetAllAsync() =>
        await _context.ChecklistItems.OrderBy(c => c.Category).ThenBy(c => c.Order).ToListAsync();

    public async Task<IEnumerable<ChecklistItem>> GetByCategoryAsync(string category) =>
        await _context.ChecklistItems
            .Where(c => c.Category.ToLower() == category.Trim().ToLowerInvariant())
            .OrderBy(c => c.Order)
            .ToListAsync();
}
