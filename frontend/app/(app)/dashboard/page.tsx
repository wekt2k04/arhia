import { redirect } from "next/navigation";
import Link from "next/link";
import { FolderKanban, Hourglass, Loader2 } from "lucide-react";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
import { listWorkflows, type WorkflowInstanceListItem } from "@/lib/api/workflows";
import { estApiError } from "@/lib/api/errors";
import { estAdminQualite, estRH } from "@/lib/roles";
import { WorkflowStatusBadge, WORKFLOW_META } from "@/components/status-badge";
import { QueryState } from "@/components/states/query-state";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { ReindexCorpusButton } from "@/components/reindex-corpus-button";
import { calculerProgression } from "@/lib/workflow-progress";
import { cn } from "@/lib/utils";

function CarteMetrique({
  icone: Icone,
  couleurCls,
  label,
  valeur,
}: {
  icone: typeof Loader2;
  couleurCls: string;
  label: string;
  valeur: number;
}) {
  return (
    <Card>
      <CardContent className="flex items-center gap-4 pt-6">
        <div className={cn("flex h-11 w-11 shrink-0 items-center justify-center rounded-full", couleurCls)}>
          <Icone className="h-5 w-5" aria-hidden />
        </div>
        <div>
          <p className="text-sm font-medium text-muted-foreground">{label}</p>
          <p className="text-3xl font-semibold leading-tight">{valeur}</p>
        </div>
      </CardContent>
    </Card>
  );
}

export default async function DashboardPage() {
  const utilisateur = await obtenirUtilisateurCourant();
  if (!utilisateur) redirect("/login");

  let items: WorkflowInstanceListItem[];
  try {
    items = await listWorkflows();
  } catch (erreur) {
    if (estApiError(erreur) && erreur.status === 401) redirect("/login");
    if (estApiError(erreur) && erreur.status === 403) return <QueryState kind="forbidden" />;
    return <QueryState kind="error" message={estApiError(erreur) ? erreur.message : undefined} />;
  }

  const enCours = items.filter((i) => i.status === 0);
  const admin = estAdminQualite(utilisateur.role);
  const rh = estRH(utilisateur.role);
  const rhOuAdmin = rh || admin;

  const repartition = (Object.keys(WORKFLOW_META) as unknown as Array<keyof typeof WORKFLOW_META>)
    .map((statut) => ({ statut, meta: WORKFLOW_META[statut], total: items.filter((i) => i.status === statut).length }))
    .filter((entree) => entree.total > 0);

  return (
    <div className="mx-auto max-w-5xl space-y-6 px-6 py-8">
      <h1 className="text-2xl font-semibold tracking-tight">Vue d&apos;ensemble</h1>

      <div className="grid gap-4 sm:grid-cols-3">
        <CarteMetrique
          icone={Loader2}
          couleurCls="bg-status-inprogress text-status-inprogress-foreground"
          label={rhOuAdmin ? "Dossiers en cours" : "Mes dossiers en cours"}
          valeur={enCours.length}
        />
        <CarteMetrique
          icone={FolderKanban}
          couleurCls="bg-primary text-primary-foreground"
          label={`Total${admin ? " (tous pôles)" : rh ? " (votre pôle)" : ""}`}
          valeur={items.length}
        />
        <CarteMetrique
          icone={Hourglass}
          couleurCls="bg-status-pending text-status-pending-foreground"
          label="Items en attente"
          valeur={items.reduce((somme, i) => somme + i.pendingItems, 0)}
        />
      </div>

      {rhOuAdmin && repartition.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {repartition.map(({ statut, meta, total }) => {
            const { Icon } = meta;
            return (
              <span
                key={statut}
                className={cn("inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium", meta.cls)}
              >
                <Icon className="h-3.5 w-3.5" aria-hidden />
                {meta.label} · {total}
              </span>
            );
          })}
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            {rhOuAdmin ? "Dossiers en cours" : "Vos dossiers"}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {enCours.length === 0 ? (
            <QueryState
              kind="empty"
              message={
                rhOuAdmin
                  ? "Aucun dossier en cours."
                  : "Aucun dossier en cours. Contactez le RH de votre pôle si vous attendiez une intégration."
              }
            />
          ) : (
            <ul className="divide-y divide-border">
              {enCours
                .slice()
                .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
                .map((item) => {
                  const progression = calculerProgression(item.totalItems, item.doneItems, item.failedItems);
                  return (
                    <li key={item.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
                      <div className="min-w-[160px]">
                        <p className="font-medium">{item.employeeFullName}</p>
                        <p className="text-sm text-muted-foreground">{item.employeeNumber}</p>
                      </div>
                      <div className="flex flex-1 min-w-[140px] max-w-[220px] items-center gap-2">
                        <Progress value={progression.pourcentage} className="flex-1" />
                        <span className="text-xs text-muted-foreground">{progression.traites}/{progression.total}</span>
                      </div>
                      <div className="flex items-center gap-3">
                        <WorkflowStatusBadge status={item.status} />
                        <Button asChild size="sm" variant="outline">
                          <Link href={`/workflows/${item.id}`}>Voir</Link>
                        </Button>
                      </div>
                    </li>
                  );
                })}
            </ul>
          )}
        </CardContent>
      </Card>

      {admin && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Administration</CardTitle>
          </CardHeader>
          <CardContent>
            <ReindexCorpusButton />
          </CardContent>
        </Card>
      )}
    </div>
  );
}
