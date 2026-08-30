namespace Arhia.Domain.ValueObjects;

public readonly record struct EmployeeNumber
{
    public string Value { get; }

    public EmployeeNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Le matricule ne peut pas être vide.", nameof(value));

        var trimmed = value.Trim();
        if (trimmed.Length > 20)
            throw new ArgumentException("Le matricule ne peut pas dépasser 20 caractères.", nameof(value));

        Value = trimmed;
    }

    public override string ToString() => Value;
}
