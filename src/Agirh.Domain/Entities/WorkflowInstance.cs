using Agirh.Domain;

namespace Agirh.Domain.Entities;

public class WorkflowInstance
{
    public Guid Id { get; }
    public Guid EmployeeId { get; }
    public Guid TemplateId { get; }
    public string TemplateVersion { get; }
    public WorkflowType Type { get; }
    public WorkflowStatus Status { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? ClosureDate { get; private set; }
    private readonly List<ChecklistItemStatus> _items;
    public IReadOnlyList<ChecklistItemStatus> Items => _items.AsReadOnly();

    public WorkflowInstance(
        Guid id,
        Guid employeeId,
        Guid templateId,
        string templateVersion,
        WorkflowType type,
        IReadOnlyCollection<ChecklistItemStatus>? items,
        DateTime createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du workflow est requis.", nameof(id));
        if (employeeId == Guid.Empty)
            throw new ArgumentException("Le collaborateur est requis.", nameof(employeeId));
        if (templateId == Guid.Empty)
            throw new ArgumentException("Le template est requis.", nameof(templateId));
        if (string.IsNullOrWhiteSpace(templateVersion))
            throw new ArgumentException("La version du template est requise.", nameof(templateVersion));

        var itemsList = items?.ToList() ?? new List<ChecklistItemStatus>();
        if (itemsList.Count == 0)
            throw new ArgumentException("Un workflow doit contenir au moins un item.", nameof(items));

        Id = id;
        EmployeeId = employeeId;
        TemplateId = templateId;
        TemplateVersion = templateVersion.Trim();
        Type = type;
        _items = itemsList;
        CreatedAt = createdAt;
        Status = WorkflowStatus.InProgress;
    }

    private WorkflowInstance(Guid id, Guid employeeId, Guid templateId, string templateVersion, WorkflowType type, DateTime createdAt)
    {
        Id = id;
        EmployeeId = employeeId;
        TemplateId = templateId;
        TemplateVersion = templateVersion;
        Type = type;
        CreatedAt = createdAt;
        Status = WorkflowStatus.InProgress;
        _items = new List<ChecklistItemStatus>();
    }

    public void Check(Guid itemId, ItemStatus status, Guid checkedBy, DateTime checkedDate, string? comment)
    {
        EnsureModifiable();

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"L'item {itemId} n'appartient pas à ce workflow.");

        item.Check(status, checkedBy, checkedDate, comment);
    }

    public void Close(DateTime closureDate)
    {
        EnsureModifiable();

        if (_items.Any(i => i.Status == ItemStatus.Pending))
            throw new InvalidOperationException("Tous les items doivent être cochés (Ok ou Ko) avant clôture.");

        Status = WorkflowStatus.Closed;
        ClosureDate = closureDate;
    }

    public void Archive()
    {
        if (Status != WorkflowStatus.Closed)
            throw new InvalidOperationException($"Seul un workflow Clôturé peut être archivé (statut actuel : {Status}).");
        Status = WorkflowStatus.Archived;
    }

    public void Cancel()
    {
        if (Status is WorkflowStatus.Archived or WorkflowStatus.Cancelled)
            throw new InvalidOperationException($"Ce workflow ne peut plus être annulé (statut actuel : {Status}).");
        Status = WorkflowStatus.Cancelled;
    }

    public void Suspend()
    {
        EnsureModifiable();
        Status = WorkflowStatus.Suspended;
    }

    public void Resume()
    {
        if (Status != WorkflowStatus.Suspended)
            throw new InvalidOperationException($"Seul un workflow Suspendu peut être repris (statut actuel : {Status}).");
        Status = WorkflowStatus.InProgress;
    }

    private void EnsureModifiable()
    {
        if (Status != WorkflowStatus.InProgress)
            throw new InvalidOperationException($"Ce workflow n'est pas modifiable dans son état actuel ({Status}) — archivé/clôturé en lecture seule.");
    }
}
