using Agirh.Domain.Entities;

namespace Agirh.Domain.Interfaces;

public interface IChecklistRepository
{
    Task<IEnumerable<ChecklistItem>> GetAllAsync();
    Task<IEnumerable<ChecklistItem>> GetByCategoryAsync(string category);
}
