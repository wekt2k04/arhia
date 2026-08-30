import { redirect } from "next/navigation";
import Link from "next/link";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
import { getEmployee } from "@/lib/api/employees";
import { listWorkflows } from "@/lib/api/workflows";
import { estApiError } from "@/lib/api/errors";
import { estAdminQualite, estRH } from "@/lib/roles";
import { WorkflowStatusBadge } from "@/components/status-badge";
import { QueryState } from "@/components/states/query-state";
import { InstantiateWorkflowButton } from "@/components/instantiate-workflow-button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";

const CONTRAT_LABEL: Record<number, string> = { 0: "CDI", 1: "CDD", 2: "Stage", 3: "Alternance" };

export default async function EmployeeDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const utilisateur = await obtenirUtilisateurCourant();
  if (!utilisateur) redirect("/login");
  const { id } = await params;

  try {
    const [employee, workflows] = await Promise.all([getEmployee(id), listWorkflows(id)]);
    const peutInstancier = estRH(utilisateur.role) || estAdminQualite(utilisateur.role);

    return (
      <div className="mx-auto max-w-3xl space-y-6 px-6 py-8">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{employee.firstName} {employee.lastName}</h1>
          <p className="text-sm text-muted-foreground">
            {employee.employeeNumber} · {employee.jobTitle} · {CONTRAT_LABEL[employee.contractType] ?? employee.contractType}
          </p>
        </div>

        {peutInstancier && (
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Instancier un dossier</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-wrap items-center gap-2">
              <InstantiateWorkflowButton employeeId={employee.id} type={0} label="Créer un Onboarding" />
              <InstantiateWorkflowButton employeeId={employee.id} type={1} label="Créer un Offboarding" />
              <p className="w-full text-xs text-muted-foreground">
                Aucun contrôle anti-doublon côté serveur : réinstancier crée un dossier supplémentaire.
              </p>
            </CardContent>
          </Card>
        )}

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Dossiers associés</CardTitle>
          </CardHeader>
          <CardContent>
            {workflows.length === 0 ? (
              <QueryState kind="empty" message="Aucun dossier pour ce collaborateur." />
            ) : (
              <ul className="divide-y divide-border">
                {workflows.map((w) => (
                  <li key={w.id} className="flex items-center justify-between gap-3 py-3">
                    <span>{w.type === 0 ? "Onboarding" : "Offboarding"}</span>
                    <div className="flex items-center gap-3">
                      <WorkflowStatusBadge status={w.status} />
                      <Button asChild size="sm" variant="outline">
                        <Link href={`/workflows/${w.id}`}>Voir</Link>
                      </Button>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    );
  } catch (erreur) {
    if (estApiError(erreur) && erreur.status === 401) redirect("/login");
    if (estApiError(erreur) && erreur.status === 403) return <QueryState kind="forbidden" />;
    if (estApiError(erreur) && erreur.status === 404) return <QueryState kind="not-found" />;
    return <QueryState kind="error" message={estApiError(erreur) ? erreur.message : undefined} />;
  }
}
