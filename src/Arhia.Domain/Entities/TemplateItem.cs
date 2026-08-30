using Arhia.Domain;

namespace Arhia.Domain.Entities;

public class TemplateItem
{
    public Guid Id { get; }
    public string Label { get; }
    public int Order { get; }
    public IReadOnlyCollection<ContractType> ApplicableContractTypes { get; }

    public TemplateItem(Guid id, string label, int order, IReadOnlyCollection<ContractType>? applicableContractTypes = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant de l'item est requis.", nameof(id));
        if (order < 0)
            throw new ArgumentException("L'ordre ne peut pas être négatif.", nameof(order));

        Id = id;
        Label = ValidateLabel(label);
        Order = order;
        ApplicableContractTypes = (applicableContractTypes ?? Enumerable.Empty<ContractType>()).ToList().AsReadOnly();
    }

    public bool IsApplicableFor(ContractType contractType) =>
        ApplicableContractTypes.Count == 0 || ApplicableContractTypes.Contains(contractType);

    private static string ValidateLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Le libellé de l'item ne peut pas être vide.", nameof(label));
        return label.Trim();
    }
}
