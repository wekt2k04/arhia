namespace Arhia.Domain;

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
    InProgress,
    Closed,
    Archived,
    Cancelled,
    Suspended
}

public enum ItemStatus
{
    Pending,
    Done,
    Failed
}

public enum TemplateStatus
{
    Draft,
    InReview,
    Approved,
    Rejected
}
