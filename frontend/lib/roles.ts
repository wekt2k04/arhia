// Miroir exact de Arhia.Domain/Enums.cs::RoleType — sérialisation JSON .NET par défaut = entier.
export enum RoleType {
  Employee = 0,
  HR = 1,
  QualityAdmin = 2,
}

const NOMS_ROLE: Record<number, string> = {
  [RoleType.Employee]: "Collaborateur",
  [RoleType.HR]: "RH",
  [RoleType.QualityAdmin]: "Admin/Qualité",
};

export const nomDuRole = (role: number): string => NOMS_ROLE[role] ?? `Rôle inconnu (${role})`;
export const estRH = (role: number) => role === RoleType.HR;
export const estAdminQualite = (role: number) => role === RoleType.QualityAdmin;
export const estCollaborateur = (role: number) => role === RoleType.Employee;

// RbacMatrix.WorkflowInstanceCheck / .Close : HR seul.
export const peutTraiterWorkflow = (role: number) => role === RoleType.HR;
// RbacMatrix.WorkflowInstanceArchive : HR + QualityAdmin.
export const peutArchiverWorkflow = (role: number) => role === RoleType.HR || role === RoleType.QualityAdmin;
