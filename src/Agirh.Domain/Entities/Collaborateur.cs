using Agirh.Domain;
using Agirh.Domain.ValueObjects;

namespace Agirh.Domain.Entities;

public class Collaborateur
{
    public Guid Id { get; }
    public Matricule Matricule { get; }
    public string Nom { get; private set; }
    public string Prenom { get; private set; }
    public string Poste { get; private set; }
    public Guid PoleId { get; private set; }
    public TypeContrat TypeContrat { get; private set; }
    public DateTime DateIntegration { get; }
    public DateTime? DateDepart { get; private set; }
    public Guid? CompteUtilisateurId { get; private set; }

    public Collaborateur(
        Guid id,
        Matricule matricule,
        string nom,
        string prenom,
        string poste,
        Guid poleId,
        TypeContrat typeContrat,
        DateTime dateIntegration)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du collaborateur est requis.", nameof(id));
        if (poleId == Guid.Empty)
            throw new ArgumentException("Le pôle du collaborateur est requis.", nameof(poleId));

        Id = id;
        Matricule = matricule;
        Nom = ValiderTexte(nom, nameof(nom));
        Prenom = ValiderTexte(prenom, nameof(prenom));
        Poste = ValiderTexte(poste, nameof(poste));
        PoleId = poleId;
        TypeContrat = typeContrat;
        DateIntegration = dateIntegration;
    }

    public void EnregistrerDepart(DateTime dateDepart)
    {
        if (dateDepart < DateIntegration)
            throw new InvalidOperationException("La date de départ ne peut pas précéder la date d'intégration.");
        DateDepart = dateDepart;
    }

    public void ChangerDePole(Guid nouveauPoleId)
    {
        if (nouveauPoleId == Guid.Empty)
            throw new ArgumentException("Le nouveau pôle est requis.", nameof(nouveauPoleId));
        PoleId = nouveauPoleId;
    }

    public void LierCompte(Guid compteUtilisateurId)
    {
        if (compteUtilisateurId == Guid.Empty)
            throw new ArgumentException("L'identifiant de compte est requis.", nameof(compteUtilisateurId));
        CompteUtilisateurId = compteUtilisateurId;
    }

    private static string ValiderTexte(string valeur, string nomParametre)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            throw new ArgumentException("La valeur ne peut pas être vide.", nomParametre);
        return valeur.Trim();
    }
}
