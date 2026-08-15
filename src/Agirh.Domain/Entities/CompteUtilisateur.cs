using Agirh.Domain;

namespace Agirh.Domain.Entities;

public class CompteUtilisateur
{
    public Guid Id { get; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public RoleType Role { get; private set; }
    public Guid? PoleId { get; private set; }
    public bool EstActif { get; private set; }
    public DateTime DateCreation { get; }

    public CompteUtilisateur(Guid id, string email, string passwordHash, RoleType role, Guid? poleId, DateTime dateCreation)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du compte est requis.", nameof(id));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Le hash de mot de passe est requis.", nameof(passwordHash));

        Id = id;
        Email = ValiderEmail(email);
        PasswordHash = passwordHash;
        Role = role;
        PoleId = ValiderPoleId(role, poleId);
        EstActif = true;
        DateCreation = dateCreation;
    }

    public void ElevRole(RoleType nouveauRole, Guid? nouveauPoleId)
    {
        Role = nouveauRole;
        PoleId = ValiderPoleId(nouveauRole, nouveauPoleId);
    }

    public void Desactiver() => EstActif = false;

    public void Reactiver() => EstActif = true;

    private static string ValiderEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("L'email est invalide.", nameof(email));
        return email.Trim().ToLowerInvariant();
    }

    private static Guid? ValiderPoleId(RoleType role, Guid? poleId)
    {
        return role switch
        {
            RoleType.RH when poleId is null || poleId == Guid.Empty =>
                throw new ArgumentException("Un compte RH doit être rattaché à un pôle.", nameof(poleId)),
            RoleType.AdminQualite when poleId is not null =>
                throw new ArgumentException("Un compte Admin/Qualité n'est rattaché à aucun pôle (portée globale).", nameof(poleId)),
            RoleType.Collaborateur when poleId is not null =>
                throw new ArgumentException("Un compte Collaborateur ne porte pas de pôle directement — il est dérivé de sa fiche Collaborateur liée.", nameof(poleId)),
            _ => poleId
        };
    }
}
