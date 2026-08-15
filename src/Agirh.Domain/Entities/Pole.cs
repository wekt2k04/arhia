namespace Agirh.Domain.Entities;

public class Pole
{
    public Guid Id { get; }
    public string Nom { get; private set; }

    public Pole(Guid id, string nom)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du pôle est requis.", nameof(id));

        Id = id;
        Nom = ValiderNom(nom);
    }

    public void Renommer(string nouveauNom) => Nom = ValiderNom(nouveauNom);

    private static string ValiderNom(string nom)
    {
        if (string.IsNullOrWhiteSpace(nom))
            throw new ArgumentException("Le nom du pôle ne peut pas être vide.", nameof(nom));
        return nom.Trim();
    }
}
