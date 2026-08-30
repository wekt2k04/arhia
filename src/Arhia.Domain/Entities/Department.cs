namespace Arhia.Domain.Entities;

public class Department
{
    public Guid Id { get; }
    public string Name { get; private set; }

    public Department(Guid id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du pôle est requis.", nameof(id));

        Id = id;
        Name = ValidateName(name);
    }

    public void Rename(string newName) => Name = ValidateName(newName);

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Le nom du pôle ne peut pas être vide.", nameof(name));
        return name.Trim();
    }
}
