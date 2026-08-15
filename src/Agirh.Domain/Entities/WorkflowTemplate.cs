using Agirh.Domain;

namespace Agirh.Domain.Entities;

public class WorkflowTemplate
{
    public Guid Id { get; }
    public WorkflowType Type { get; }
    public string Version { get; }
    public TemplateStatut Statut { get; private set; }
    public Guid RedacteurId { get; }
    public Guid? VerificateurId { get; private set; }
    public Guid? ApprobateurId { get; private set; }
    public string? MotifRejet { get; private set; }
    public DateTime DateCreation { get; }
    private readonly List<TemplateSection> _sections;
    public IReadOnlyList<TemplateSection> Sections => _sections.AsReadOnly();

    public WorkflowTemplate(
        Guid id,
        WorkflowType type,
        string version,
        Guid redacteurId,
        IReadOnlyCollection<TemplateSection>? sections,
        DateTime dateCreation)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du template est requis.", nameof(id));
        if (redacteurId == Guid.Empty)
            throw new ArgumentException("Le rédacteur est requis.", nameof(redacteurId));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("La version ne peut pas être vide.", nameof(version));

        var sectionsList = sections?.ToList() ?? new List<TemplateSection>();
        if (sectionsList.Count == 0)
            throw new ArgumentException("Un template doit contenir au moins une section.", nameof(sections));

        Id = id;
        Type = type;
        Version = version.Trim();
        RedacteurId = redacteurId;
        _sections = sectionsList;
        DateCreation = dateCreation;
        Statut = TemplateStatut.Brouillon;
    }

    private WorkflowTemplate(Guid id, WorkflowType type, string version, Guid redacteurId, DateTime dateCreation)
    {
        Id = id;
        Type = type;
        Version = version;
        RedacteurId = redacteurId;
        DateCreation = dateCreation;
        Statut = TemplateStatut.Brouillon;
        _sections = new List<TemplateSection>();
    }

    public void Soumettre()
    {
        if (Statut != TemplateStatut.Brouillon)
            throw new InvalidOperationException($"Seul un template Brouillon peut être soumis (statut actuel : {Statut}).");
        Statut = TemplateStatut.EnValidation;
    }

    public void Verifier(Guid verificateurId)
    {
        if (Statut != TemplateStatut.EnValidation)
            throw new InvalidOperationException($"Seul un template En Validation peut être vérifié (statut actuel : {Statut}).");
        if (verificateurId == RedacteurId)
            throw new InvalidOperationException("Le rédacteur ne peut pas vérifier son propre template.");
        if (VerificateurId is not null)
            throw new InvalidOperationException("Ce template a déjà été vérifié.");

        VerificateurId = verificateurId;
    }

    public void Approuver(Guid approbateurId)
    {
        if (Statut != TemplateStatut.EnValidation)
            throw new InvalidOperationException($"Seul un template En Validation peut être approuvé (statut actuel : {Statut}).");
        if (VerificateurId is null)
            throw new InvalidOperationException("Le template doit être vérifié avant d'être approuvé.");
        if (approbateurId == VerificateurId)
            throw new InvalidOperationException("L'approbateur doit être distinct du vérificateur.");
        if (approbateurId == RedacteurId)
            throw new InvalidOperationException("Le rédacteur ne peut pas approuver son propre template.");

        ApprobateurId = approbateurId;
        Statut = TemplateStatut.Approuve;
    }

    public void Rejeter(Guid acteurId, string motif)
    {
        if (Statut != TemplateStatut.EnValidation)
            throw new InvalidOperationException($"Seul un template En Validation peut être rejeté (statut actuel : {Statut}).");
        if (string.IsNullOrWhiteSpace(motif))
            throw new ArgumentException("Un motif de rejet est requis.", nameof(motif));

        Statut = TemplateStatut.Rejete;
        MotifRejet = motif.Trim();
    }

    public IReadOnlyCollection<TemplateItem> ResoudreItemsApplicables(TypeContrat typeContrat) =>
        _sections
            .SelectMany(s => s.Items)
            .Where(i => i.ApplicablePour(typeContrat))
            .ToList()
            .AsReadOnly();
}
