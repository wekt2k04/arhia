import { redirect } from "next/navigation";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
import { getWorkflowInstance } from "@/lib/api/workflows";
import { estApiError } from "@/lib/api/errors";
import { calculerProgression } from "@/lib/workflow-progress";
import { WorkflowStatusBadge, ItemStatusBadge } from "@/components/status-badge";
import { QueryState } from "@/components/states/query-state";
import { WorkflowActions } from "@/components/workflow-actions";
import { Card, CardContent } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";

const TYPE_LABEL: Record<number, string> = { 0: "Onboarding", 1: "Offboarding" };

export default async function WorkflowDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const utilisateur = await obtenirUtilisateurCourant();
  if (!utilisateur) redirect("/login");

  const { id } = await params;

  try {
    const detail = await getWorkflowInstance(id);
    const totalItems = detail.sections.reduce((s, sec) => s + sec.items.length, 0);
    const doneItems = detail.sections.reduce((s, sec) => s + sec.items.filter((i) => i.status === 1).length, 0);
    const failedItems = detail.sections.reduce((s, sec) => s + sec.items.filter((i) => i.status === 2).length, 0);
    const progression = calculerProgression(totalItems, doneItems, failedItems);

    return (
      <div className="mx-auto max-w-3xl space-y-6 px-6 py-8">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{detail.employeeFullName}</h1>
          <p className="text-sm text-muted-foreground">
            {detail.employeeNumber} · {TYPE_LABEL[detail.type] ?? detail.type} · version {detail.templateVersion}
          </p>
        </div>

        <Card>
          <CardContent className="flex flex-wrap items-center gap-4 pt-6">
            <WorkflowStatusBadge status={detail.status} />
            <span className="text-sm text-muted-foreground">
              Créé le {new Date(detail.createdAt).toLocaleDateString("fr-FR")}
              {detail.closureDate ? ` · Clôturé le ${new Date(detail.closureDate).toLocaleDateString("fr-FR")}` : ""}
            </span>
            <div className="flex flex-1 min-w-[160px] items-center gap-2">
              <Progress value={progression.pourcentage} className="flex-1" />
              <span className="text-xs text-muted-foreground">{progression.traites}/{progression.total}</span>
            </div>
          </CardContent>
        </Card>

        <WorkflowActions
          workflowId={detail.id}
          status={detail.status}
          role={utilisateur.role}
          sections={detail.sections}
        />

        <div className="space-y-3">
          {detail.sections.map((section) => (
            <details key={section.name} open className="rounded-lg border border-border">
              <summary className="cursor-pointer select-none px-4 py-3 font-medium">
                {section.name} <span className="text-sm font-normal text-muted-foreground">({section.items.length})</span>
              </summary>
              <ul className="divide-y divide-border border-t border-border">
                {section.items.map((item) => (
                  <li key={item.id} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3">
                    <div>
                      <p>{item.label}</p>
                      {item.comment && <p className="text-sm text-muted-foreground">{item.comment}</p>}
                      {item.checkedDate && (
                        <p className="text-xs text-muted-foreground">
                          traité le {new Date(item.checkedDate).toLocaleDateString("fr-FR")}
                        </p>
                      )}
                    </div>
                    <ItemStatusBadge status={item.status} />
                  </li>
                ))}
              </ul>
            </details>
          ))}
        </div>
      </div>
    );
  } catch (erreur) {
    if (estApiError(erreur) && erreur.status === 401) redirect("/login");
    if (estApiError(erreur) && erreur.status === 403) return <QueryState kind="forbidden" />;
    if (estApiError(erreur) && erreur.status === 404) return <QueryState kind="not-found" />;
    return <QueryState kind="error" message={estApiError(erreur) ? erreur.message : undefined} />;
  }
}
