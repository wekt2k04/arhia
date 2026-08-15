using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.Security;

public static class PoleScopeGuard
{
    public static bool PeutAccederAuPole(CompteUtilisateur acteur, Guid poleCibleId)
    {
        return acteur.Role switch
        {
            RoleType.AdminQualite => true,
            RoleType.RH => acteur.PoleId == poleCibleId,
            _ => false
        };
    }

    public static bool PeutAccederAuCollaborateur(CompteUtilisateur acteur, Collaborateur cible)
    {
        return acteur.Role switch
        {
            RoleType.AdminQualite => true,
            RoleType.RH => acteur.PoleId == cible.PoleId,
            RoleType.Collaborateur => acteur.Id == cible.CompteUtilisateurId,
            _ => false
        };
    }
}
