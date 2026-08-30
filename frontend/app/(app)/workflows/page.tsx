import { redirect } from "next/navigation";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
import { listWorkflows } from "@/lib/api/workflows";
import { estApiError } from "@/lib/api/errors";
import { QueryState } from "@/components/states/query-state";
import { WorkflowsExplorer } from "@/components/workflows-explorer";

export default async function WorkflowsPage() {
  const utilisateur = await obtenirUtilisateurCourant();
  if (!utilisateur) redirect("/login");

  try {
    const items = await listWorkflows();
    return (
      <div className="mx-auto max-w-5xl space-y-4 px-6 py-8">
        <h1 className="text-2xl font-semibold tracking-tight">Dossiers</h1>
        <WorkflowsExplorer items={items} />
      </div>
    );
  } catch (erreur) {
    if (estApiError(erreur) && erreur.status === 401) redirect("/login");
    if (estApiError(erreur) && erreur.status === 403) return <QueryState kind="forbidden" />;
    return <QueryState kind="error" message={estApiError(erreur) ? erreur.message : undefined} />;
  }
}
