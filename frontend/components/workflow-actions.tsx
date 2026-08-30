"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { peutArchiverWorkflow, peutTraiterWorkflow } from "@/lib/roles";
import type { ChecklistSection } from "@/lib/api/workflows";

const STATUT_EN_COURS = 0;
const STATUT_CLOTURE = 1;

async function appelerBff(chemin: string, corps?: unknown): Promise<{ ok: boolean; message?: string }> {
  const reponse = await fetch(chemin, {
    method: "POST",
    headers: corps ? { "Content-Type": "application/json" } : undefined,
    body: corps ? JSON.stringify(corps) : undefined,
  });
  if (reponse.ok) return { ok: true };
  const donnees = await reponse.json().catch(() => ({}));
  return { ok: false, message: donnees.erreur ?? `Échec (${reponse.status}).` };
}

export function WorkflowActions({
  workflowId,
  status,
  role,
  sections,
}: {
  workflowId: string;
  status: number;
  role: number;
  sections: ChecklistSection[];
}) {
  const router = useRouter();
  const [enCours, setEnCours] = useState<string | null>(null);

  const peutTraiter = peutTraiterWorkflow(role) && status === STATUT_EN_COURS;
  const peutArchiver = peutArchiverWorkflow(role) && status === STATUT_CLOTURE;
  const itemsEnAttente = sections.flatMap((s) => s.items).filter((i) => i.status === 0);

  async function cocherItem(itemId: string, statut: 1 | 2) {
    setEnCours(itemId);
    const { ok, message } = await appelerBff(`/api/workflows/${workflowId}/items/${itemId}/check`, { status: statut, comment: null });
    setEnCours(null);
    if (ok) {
      toast.success("Item mis à jour.");
      router.refresh();
    } else {
      toast.error(message);
    }
  }

  async function cloturer() {
    setEnCours("close");
    const { ok, message } = await appelerBff(`/api/workflows/${workflowId}/close`);
    setEnCours(null);
    if (ok) {
      toast.success("Dossier clôturé.");
      router.refresh();
    } else {
      toast.error(message);
    }
  }

  async function archiver() {
    setEnCours("archive");
    const { ok, message } = await appelerBff(`/api/workflows/${workflowId}/archive`);
    setEnCours(null);
    if (ok) {
      toast.success("Dossier archivé.");
      router.refresh();
    } else {
      toast.error(message);
    }
  }

  if (!peutTraiter && !peutArchiver) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Actions</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {peutTraiter && itemsEnAttente.length > 0 && (
          <div className="space-y-2">
            <p className="text-sm text-muted-foreground">Items en attente ({itemsEnAttente.length})</p>
            <ul className="space-y-2">
              {itemsEnAttente.map((item) => (
                <li key={item.id} className="flex items-center justify-between gap-2 rounded-md border border-border px-3 py-2">
                  <span className="text-sm">{item.label}</span>
                  <div className="flex gap-2">
                    <Button size="sm" variant="outline" disabled={enCours === item.id} onClick={() => cocherItem(item.id, 1)}>
                      Ok
                    </Button>
                    <Button size="sm" variant="outline" disabled={enCours === item.id} onClick={() => cocherItem(item.id, 2)}>
                      Ko
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        )}
        <div className="flex gap-2">
          {peutTraiter && (
            <Button disabled={enCours === "close"} onClick={cloturer}>
              Clôturer le dossier
            </Button>
          )}
          {peutArchiver && (
            <Button variant="secondary" disabled={enCours === "archive"} onClick={archiver}>
              Archiver
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
