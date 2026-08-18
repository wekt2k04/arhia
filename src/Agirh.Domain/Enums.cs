namespace Agirh.Domain;

public enum RoleType
{
    Employee,
    HR,
    QualityAdmin
}

public enum ContractType
{
    CDI,
    CDD,
    Stage,
    Alternance
}

public enum WorkflowType
{
    Onboarding,
    Offboarding
}

public enum WorkflowStatus
{
    EnCours,
    Cloture,
    Archive,
    Annule,
    Suspendu
}

public enum ItemEtat
{
    EnAttente,
    Ok,
    Ko
}

public enum TemplateStatut
{
    Brouillon,
    EnValidation,
    Approuve,
    Rejete
}
