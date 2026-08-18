namespace Agirh.Domain.Entities;

public class TemplateSection
{
    public Guid Id { get; }
    public string Name { get; }
    public int Order { get; }
    private readonly List<TemplateItem> _items;
    public IReadOnlyList<TemplateItem> Items => _items.AsReadOnly();

    public TemplateSection(Guid id, string name, int order, IReadOnlyCollection<TemplateItem>? items)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant de la section est requis.", nameof(id));
        if (order < 0)
            throw new ArgumentException("L'ordre ne peut pas être négatif.", nameof(order));

        var itemsList = items?.ToList() ?? new List<TemplateItem>();
        if (itemsList.Count == 0)
            throw new ArgumentException("Une section doit contenir au moins un item.", nameof(items));

        Id = id;
        Name = ValidateName(name);
        Order = order;
        _items = itemsList;
    }

    private TemplateSection(Guid id, string name, int order)
    {
        Id = id;
        Name = name;
        Order = order;
        _items = new List<TemplateItem>();
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Le nom de la section ne peut pas être vide.", nameof(name));
        return name.Trim();
    }
}
