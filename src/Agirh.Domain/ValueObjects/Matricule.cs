namespace Agirh.Domain.ValueObjects;

public readonly record struct Matricule
{
    public string Valeur { get; }

    public Matricule(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            throw new ArgumentException("Le matricule ne peut pas être vide.", nameof(valeur));

        var trimmed = valeur.Trim();
        if (trimmed.Length > 20)
            throw new ArgumentException("Le matricule ne peut pas dépasser 20 caractères.", nameof(valeur));

        Valeur = trimmed;
    }

    public override string ToString() => Valeur;
}
