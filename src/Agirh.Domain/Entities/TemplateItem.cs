using Agirh.Domain;

namespace Agirh.Domain.Entities;

public class TemplateItem
{
    public Guid Id { get; }
    public string Libelle { get; }
    public int Ordre { get; }
    public IReadOnlyCollection<ContractType> ConditionsTypeContrat { get; }

    public TemplateItem(Guid id, string libelle, int ordre, IReadOnlyCollection<ContractType>? conditionsTypeContrat = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant de l'item est requis.", nameof(id));
        if (ordre < 0)
            throw new ArgumentException("L'ordre ne peut pas être négatif.", nameof(ordre));

        Id = id;
        Libelle = ValiderLibelle(libelle);
        Ordre = ordre;
        ConditionsTypeContrat = (conditionsTypeContrat ?? Enumerable.Empty<ContractType>()).ToList().AsReadOnly();
    }

    public bool ApplicablePour(ContractType typeContrat) =>
        ConditionsTypeContrat.Count == 0 || ConditionsTypeContrat.Contains(typeContrat);

    private static string ValiderLibelle(string libelle)
    {
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé de l'item ne peut pas être vide.", nameof(libelle));
        return libelle.Trim();
    }
}
