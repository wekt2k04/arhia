namespace Agirh.Domain.Entities;

public class TemplateSection
{
    public Guid Id { get; }
    public string Nom { get; }
    public int Ordre { get; }
    private readonly List<TemplateItem> _items;
    public IReadOnlyList<TemplateItem> Items => _items.AsReadOnly();

    public TemplateSection(Guid id, string nom, int ordre, IReadOnlyCollection<TemplateItem>? items)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant de la section est requis.", nameof(id));
        if (ordre < 0)
            throw new ArgumentException("L'ordre ne peut pas être négatif.", nameof(ordre));

        var itemsList = items?.ToList() ?? new List<TemplateItem>();
        if (itemsList.Count == 0)
            throw new ArgumentException("Une section doit contenir au moins un item.", nameof(items));

        Id = id;
        Nom = ValiderNom(nom);
        Ordre = ordre;
        _items = itemsList;
    }

    private TemplateSection(Guid id, string nom, int ordre)
    {
        Id = id;
        Nom = nom;
        Ordre = ordre;
        _items = new List<TemplateItem>();
    }

    private static string ValiderNom(string nom)
    {
        if (string.IsNullOrWhiteSpace(nom))
            throw new ArgumentException("Le nom de la section ne peut pas être vide.", nameof(nom));
        return nom.Trim();
    }
}
