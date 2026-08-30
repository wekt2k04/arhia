import { redirect } from "next/navigation";
import Link from "next/link";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
import { listEmployees } from "@/lib/api/employees";
import { listDepartments } from "@/lib/api/departments";
import { estApiError } from "@/lib/api/errors";
import { QueryState } from "@/components/states/query-state";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { estAdminQualite, estRH } from "@/lib/roles";
import { cn } from "@/lib/utils";

const CONTRAT_META: Record<number, { label: string; cls: string }> = {
  0: { label: "CDI", cls: "border-transparent bg-primary/15 text-primary" },
  1: { label: "CDD", cls: "border-transparent bg-sky-500/15 text-sky-700 dark:text-sky-300" },
  2: { label: "Stage", cls: "border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300" },
  3: { label: "Alternance", cls: "border-transparent bg-amber-500/15 text-amber-700 dark:text-amber-300" },
};

export default async function EmployeesPage() {
  const utilisateur = await obtenirUtilisateurCourant();
  if (!utilisateur) redirect("/login");

  try {
    const [employees, departments] = await Promise.all([listEmployees(), listDepartments()]);
    const nomDepartement = new Map(departments.map((d) => [d.id, d.name]));
    const peutCreer = estRH(utilisateur.role) || estAdminQualite(utilisateur.role);

    return (
      <div className="mx-auto max-w-5xl space-y-4 px-6 py-8">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-semibold tracking-tight">Collaborateurs</h1>
          {peutCreer && (
            <Button asChild size="sm">
              <Link href="/employees/new">Nouveau collaborateur</Link>
            </Button>
          )}
        </div>

        {employees.length === 0 ? (
          <QueryState kind="empty" message="Aucun collaborateur visible." />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Nom</TableHead>
                <TableHead>Matricule</TableHead>
                <TableHead>Poste</TableHead>
                <TableHead>Pôle</TableHead>
                <TableHead>Contrat</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {employees.map((e) => {
                const contrat = CONTRAT_META[e.contractType];
                return (
                <TableRow key={e.id}>
                  <TableCell className="font-medium">
                    <div className="flex items-center gap-2.5">
                      <Avatar className="h-8 w-8">
                        <AvatarFallback className="bg-accent text-xs text-accent-foreground">
                          {e.firstName.charAt(0)}{e.lastName.charAt(0)}
                        </AvatarFallback>
                      </Avatar>
                      {e.firstName} {e.lastName}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">{e.employeeNumber}</TableCell>
                  <TableCell>{e.jobTitle}</TableCell>
                  <TableCell>{nomDepartement.get(e.departmentId) ?? e.departmentId}</TableCell>
                  <TableCell>
                    <Badge className={cn(contrat?.cls)}>{contrat?.label ?? e.contractType}</Badge>
                  </TableCell>
                  <TableCell>
                    <Button asChild size="sm" variant="outline">
                      <Link href={`/employees/${e.id}`}>Voir</Link>
                    </Button>
                  </TableCell>
                </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </div>
    );
  } catch (erreur) {
    if (estApiError(erreur) && erreur.status === 401) redirect("/login");
    if (estApiError(erreur) && erreur.status === 403) return <QueryState kind="forbidden" />;
    return <QueryState kind="error" message={estApiError(erreur) ? erreur.message : undefined} />;
  }
}
