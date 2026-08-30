using Arhia.Domain;

namespace Arhia.Domain.Entities;

public class UserAccount
{
    public Guid Id { get; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public RoleType Role { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; }

    public UserAccount(Guid id, string email, string passwordHash, RoleType role, Guid? departmentId, DateTime createdAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du compte est requis.", nameof(id));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Le hash de mot de passe est requis.", nameof(passwordHash));

        Id = id;
        Email = ValidateEmail(email);
        PasswordHash = passwordHash;
        Role = role;
        DepartmentId = ValidateDepartmentId(role, departmentId);
        IsActive = true;
        CreatedAt = createdAt;
    }

    public void ElevateRole(RoleType newRole, Guid? newDepartmentId)
    {
        Role = newRole;
        DepartmentId = ValidateDepartmentId(newRole, newDepartmentId);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private static string ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("L'email est invalide.", nameof(email));
        return email.Trim().ToLowerInvariant();
    }

    private static Guid? ValidateDepartmentId(RoleType role, Guid? departmentId)
    {
        return role switch
        {
            RoleType.HR when departmentId is null || departmentId == Guid.Empty =>
                throw new ArgumentException("Un compte RH doit être rattaché à un pôle.", nameof(departmentId)),
            RoleType.QualityAdmin when departmentId is not null =>
                throw new ArgumentException("Un compte Admin/Qualité n'est rattaché à aucun pôle (portée globale).", nameof(departmentId)),
            RoleType.Employee when departmentId is not null =>
                throw new ArgumentException("Un compte Collaborateur ne porte pas de pôle directement — il est dérivé de sa fiche Collaborateur liée.", nameof(departmentId)),
            _ => departmentId
        };
    }
}
