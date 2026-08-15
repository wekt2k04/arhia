using Agirh.Domain;

namespace Agirh.Domain.Entities;

public class WorkflowInstance
{
    public Guid Id { get; }
    public Guid CollaborateurId { get; }
    public Guid TemplateId { get; }
    public string TemplateVersion { get; }
    public WorkflowType Type { get; }
    public WorkflowStatus Statut { get; private set; }
    public DateTime DateCreation { get; }
    public DateTime? DateCloture { get; private set; }
    private readonly List<ChecklistItemStatus> _items;
    public IReadOnlyList<ChecklistItemStatus> Items => _items.AsReadOnly();

    public WorkflowInstance(
        Guid id,
        Guid collaborateurId,
        Guid templateId,
        string templateVersion,
        WorkflowType type,
        IReadOnlyCollection<ChecklistItemStatus>? items,
        DateTime dateCreation)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du workflow est requis.", nameof(id));
        if (collaborateurId == Guid.Empty)
            throw new ArgumentException("Le collaborateur est requis.", nameof(collaborateurId));
        if (templateId == Guid.Empty)
            throw new ArgumentException("Le template est requis.", nameof(templateId));
        if (string.IsNullOrWhiteSpace(templateVersion))
            throw new ArgumentException("La version du template est requise.", nameof(templateVersion));

        var itemsList = items?.ToList() ?? new List<ChecklistItemStatus>();
        if (itemsList.Count == 0)
            throw new ArgumentException("Un workflow doit contenir au moins un item.", nameof(items));

        Id = id;
        CollaborateurId = collaborateurId;
        TemplateId = templateId;
        TemplateVersion = templateVersion.Trim();
        Type = type;
        _items = itemsList;
        DateCreation = dateCreation;
        Statut = WorkflowStatus.EnCours;
    }

    private WorkflowInstance(Guid id, Guid collaborateurId, Guid templateId, string templateVersion, WorkflowType type, DateTime dateCreation)
    {
        Id = id;
        CollaborateurId = collaborateurId;
        TemplateId = templateId;
        TemplateVersion = templateVersion;
        Type = type;
        DateCreation = dateCreation;
        Statut = WorkflowStatus.EnCours;
        _items = new List<ChecklistItemStatus>();
    }

    public void Cocher(Guid itemId, ItemEtat etat, Guid cochePar, DateTime dateCoche, string? commentaire)
    {
        GarantirModifiable();

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"L'item {itemId} n'appartient pas à ce workflow.");

        item.Cocher(etat, cochePar, dateCoche, commentaire);
    }

    public void Cloturer(DateTime dateCloture)
    {
        GarantirModifiable();

        if (_items.Any(i => i.Etat == ItemEtat.EnAttente))
            throw new InvalidOperationException("Tous les items doivent être cochés (Ok ou Ko) avant clôture.");

        Statut = WorkflowStatus.Cloture;
        DateCloture = dateCloture;
    }

    public void Archiver()
    {
        if (Statut != WorkflowStatus.Cloture)
            throw new InvalidOperationException($"Seul un workflow Clôturé peut être archivé (statut actuel : {Statut}).");
        Statut = WorkflowStatus.Archive;
    }

    public void Annuler()
    {
        if (Statut is WorkflowStatus.Archive or WorkflowStatus.Annule)
            throw new InvalidOperationException($"Ce workflow ne peut plus être annulé (statut actuel : {Statut}).");
        Statut = WorkflowStatus.Annule;
    }

    public void Suspendre()
    {
        GarantirModifiable();
        Statut = WorkflowStatus.Suspendu;
    }

    public void Reprendre()
    {
        if (Statut != WorkflowStatus.Suspendu)
            throw new InvalidOperationException($"Seul un workflow Suspendu peut être repris (statut actuel : {Statut}).");
        Statut = WorkflowStatus.EnCours;
    }

    private void GarantirModifiable()
    {
        if (Statut != WorkflowStatus.EnCours)
            throw new InvalidOperationException($"Ce workflow n'est pas modifiable dans son état actuel ({Statut}) — archivé/clôturé en lecture seule.");
    }
}
