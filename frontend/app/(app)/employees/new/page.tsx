import { redirect } from "next/navigation";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
import { listDepartments } from "@/lib/api/departments";
import { estApiError } from "@/lib/api/errors";
import { estAdminQualite, estRH } from "@/lib/roles";
import { QueryState } from "@/components/states/query-state";
import { EmployeeForm } from "@/components/employee-form";

export default async function NewEmployeePage() {
  const utilisateur = await obtenirUtilisateurCourant();
  if (!utilisateur) redirect("/login");
  if (!estRH(utilisateur.role) && !estAdminQualite(utilisateur.role)) {
    return <QueryState kind="forbidden" />;
  }

  try {
    const departments = await listDepartments();
    return (
      <div className="mx-auto max-w-3xl space-y-4 px-6 py-8">
        <h1 className="text-2xl font-semibold tracking-tight">Nouveau collaborateur</h1>
        <EmployeeForm departments={departments} />
      </div>
    );
  } catch (erreur) {
    if (estApiError(erreur) && erreur.status === 401) redirect("/login");
    return <QueryState kind="error" message={estApiError(erreur) ? erreur.message : undefined} />;
  }
}
