using Arhia.Domain;

namespace Arhia.Domain.Entities;

public class ChecklistItemStatus
{
    public Guid Id { get; }
    public Guid TemplateItemId { get; }
    public string Label { get; }
    public ItemStatus Status { get; private set; }
    public string? Comment { get; private set; }
    public Guid? CheckedBy { get; private set; }
    public DateTime? CheckedDate { get; private set; }

    public ChecklistItemStatus(Guid id, Guid templateItemId, string label)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du statut d'item est requis.", nameof(id));
        if (templateItemId == Guid.Empty)
            throw new ArgumentException("L'identifiant de l'item de template est requis.", nameof(templateItemId));
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Le libellé ne peut pas être vide.", nameof(label));

        Id = id;
        TemplateItemId = templateItemId;
        Label = label.Trim();
        Status = ItemStatus.Pending;
    }

    public void Check(ItemStatus status, Guid checkedBy, DateTime checkedDate, string? comment)
    {
        if (status == ItemStatus.Pending)
            throw new ArgumentException("Cocher un item doit produire un état Ok ou Ko, jamais EnAttente.", nameof(status));
        if (checkedBy == Guid.Empty)
            throw new ArgumentException("Le compte ayant coché l'item est requis.", nameof(checkedBy));

        Status = status;
        CheckedBy = checkedBy;
        CheckedDate = checkedDate;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }
}
