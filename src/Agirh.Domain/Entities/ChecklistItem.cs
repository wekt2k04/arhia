namespace Agirh.Domain.Entities;

public class ChecklistItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public int Order { get; set; }
}
