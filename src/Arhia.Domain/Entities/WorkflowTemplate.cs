using Arhia.Domain;

namespace Arhia.Domain.Entities;

public class WorkflowTemplate
{
    public Guid Id { get; }
    public WorkflowType Type { get; }
    public string Version { get; }
    public TemplateStatus Status { get; private set; }
    public Guid AuthorId { get; }
    public Guid? VerifierId { get; private set; }
    public Guid? ApproverId { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; }
    private readonly List<TemplateSection> _sections;
    public IReadOnlyList<TemplateSection> Sections => _sections.AsReadOnly();

    public WorkflowTemplate(
        Guid id,
        WorkflowType type,
        string version,
        Guid authorId,
        IReadOnlyCollection<TemplateSection>? sections,
        DateTime createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du template est requis.", nameof(id));
        if (authorId == Guid.Empty)
            throw new ArgumentException("Le rédacteur est requis.", nameof(authorId));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("La version ne peut pas être vide.", nameof(version));

        var sectionsList = sections?.ToList() ?? new List<TemplateSection>();
        if (sectionsList.Count == 0)
            throw new ArgumentException("Un template doit contenir au moins une section.", nameof(sections));

        Id = id;
        Type = type;
        Version = version.Trim();
        AuthorId = authorId;
        _sections = sectionsList;
        CreatedAt = createdAt;
        Status = TemplateStatus.Draft;
    }

    private WorkflowTemplate(Guid id, WorkflowType type, string version, Guid authorId, DateTime createdAt)
    {
        Id = id;
        Type = type;
        Version = version;
        AuthorId = authorId;
        CreatedAt = createdAt;
        Status = TemplateStatus.Draft;
        _sections = new List<TemplateSection>();
    }

    public void Submit()
    {
        if (Status != TemplateStatus.Draft)
            throw new InvalidOperationException($"Seul un template Brouillon peut être soumis (statut actuel : {Status}).");
        Status = TemplateStatus.InReview;
    }

    public void Verify(Guid verifierId)
    {
        if (Status != TemplateStatus.InReview)
            throw new InvalidOperationException($"Seul un template En Validation peut être vérifié (statut actuel : {Status}).");
        if (verifierId == AuthorId)
            throw new InvalidOperationException("Le rédacteur ne peut pas vérifier son propre template.");
        if (VerifierId is not null)
            throw new InvalidOperationException("Ce template a déjà été vérifié.");

        VerifierId = verifierId;
    }

    public void Approve(Guid approverId)
    {
        if (Status != TemplateStatus.InReview)
            throw new InvalidOperationException($"Seul un template En Validation peut être approuvé (statut actuel : {Status}).");
        if (VerifierId is null)
            throw new InvalidOperationException("Le template doit être vérifié avant d'être approuvé.");
        if (approverId == VerifierId)
            throw new InvalidOperationException("L'approbateur doit être distinct du vérificateur.");
        if (approverId == AuthorId)
            throw new InvalidOperationException("Le rédacteur ne peut pas approuver son propre template.");

        ApproverId = approverId;
        Status = TemplateStatus.Approved;
    }

    public void Reject(Guid actorId, string reason)
    {
        if (Status != TemplateStatus.InReview)
            throw new InvalidOperationException($"Seul un template En Validation peut être rejeté (statut actuel : {Status}).");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Un motif de rejet est requis.", nameof(reason));

        Status = TemplateStatus.Rejected;
        RejectionReason = reason.Trim();
    }

    public IReadOnlyCollection<TemplateItem> ResolveApplicableItems(ContractType contractType) =>
        _sections
            .SelectMany(s => s.Items)
            .Where(i => i.IsApplicableFor(contractType))
            .ToList()
            .AsReadOnly();
}
