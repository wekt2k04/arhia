"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";

export function InstantiateWorkflowButton({ employeeId, type, label }: { employeeId: string; type: 0 | 1; label: string }) {
  const router = useRouter();
  const [enCours, setEnCours] = useState(false);

  async function instancier() {
    setEnCours(true);
    const reponse = await fetch("/api/workflows", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ employeeId, type }),
    });
    setEnCours(false);
    if (reponse.ok) {
      toast.success("Dossier créé.");
      router.refresh();
    } else {
      const donnees = await reponse.json().catch(() => ({}));
      toast.error(donnees.erreur ?? `Échec (${reponse.status}).`);
    }
  }

  return (
    <Button size="sm" variant="outline" disabled={enCours} onClick={instancier}>
      {label}
    </Button>
  );
}
