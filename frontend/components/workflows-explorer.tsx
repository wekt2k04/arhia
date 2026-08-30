"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import type { WorkflowInstanceListItem } from "@/lib/api/workflows";
import { WorkflowStatusBadge } from "@/components/status-badge";
import { QueryState } from "@/components/states/query-state";
import { Progress } from "@/components/ui/progress";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { calculerProgression } from "@/lib/workflow-progress";

const TYPE_LABEL: Record<number, string> = { 0: "Onboarding", 1: "Offboarding" };

export function WorkflowsExplorer({ items }: { items: WorkflowInstanceListItem[] }) {
  const [recherche, setRecherche] = useState("");

  const filtres = useMemo(() => {
    const q = recherche.trim().toLowerCase();
    if (!q) return items;
    return items.filter(
      (i) => i.employeeFullName.toLowerCase().includes(q) || i.employeeNumber.toLowerCase().includes(q)
    );
  }, [items, recherche]);

  if (items.length === 0) {
    return <QueryState kind="empty" message="Aucun dossier pour le moment." />;
  }

  return (
    <div className="space-y-3">
      <Input
        placeholder="Rechercher par nom ou matricule…"
        value={recherche}
        onChange={(e) => setRecherche(e.target.value)}
        className="max-w-xs"
      />

      {filtres.length === 0 ? (
        <QueryState kind="empty" message="Aucun résultat pour cette recherche." />
      ) : (
        <>
          {/* Desktop : table dense */}
          <div className="hidden md:block">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Collaborateur</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Statut</TableHead>
                  <TableHead>Progression</TableHead>
                  <TableHead>Créé le</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtres.map((item) => {
                  const progression = calculerProgression(item.totalItems, item.doneItems, item.failedItems);
                  return (
                    <TableRow key={item.id}>
                      <TableCell>
                        <div className="font-medium">{item.employeeFullName}</div>
                        <div className="text-xs text-muted-foreground">{item.employeeNumber}</div>
                      </TableCell>
                      <TableCell>{TYPE_LABEL[item.type] ?? item.type}</TableCell>
                      <TableCell><WorkflowStatusBadge status={item.status} /></TableCell>
                      <TableCell className="w-40">
                        <div className="flex items-center gap-2">
                          <Progress value={progression.pourcentage} className="w-24" />
                          <span className="text-xs text-muted-foreground">{progression.traites}/{progression.total}</span>
                        </div>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {new Date(item.createdAt).toLocaleDateString("fr-FR")}
                      </TableCell>
                      <TableCell>
                        <Button asChild size="sm" variant="outline">
                          <Link href={`/workflows/${item.id}`}>Voir</Link>
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>

          {/* Mobile : cartes empilées */}
          <ul className="space-y-3 md:hidden">
            {filtres.map((item) => {
              const progression = calculerProgression(item.totalItems, item.doneItems, item.failedItems);
              return (
                <li key={item.id} className="rounded-lg border border-border p-4">
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <p className="font-medium">{item.employeeFullName}</p>
                      <p className="text-xs text-muted-foreground">{item.employeeNumber} · {TYPE_LABEL[item.type] ?? item.type}</p>
                    </div>
                    <WorkflowStatusBadge status={item.status} />
                  </div>
                  <div className="mt-3 flex items-center gap-2">
                    <Progress value={progression.pourcentage} className="flex-1" />
                    <span className="text-xs text-muted-foreground">{progression.traites}/{progression.total}</span>
                  </div>
                  <Button asChild size="sm" variant="outline" className="mt-3 w-full">
                    <Link href={`/workflows/${item.id}`}>Voir le dossier</Link>
                  </Button>
                </li>
              );
            })}
          </ul>
        </>
      )}
    </div>
  );
}
