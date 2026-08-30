"use client";

import { useState } from "react";
import { RefreshCw } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export function ReindexCorpusButton() {
  const [enCours, setEnCours] = useState(false);

  async function relancerIngestion() {
    setEnCours(true);
    const reponse = await fetch("/api/admin/reindex-corpus", { method: "POST" });
    const donnees = await reponse.json().catch(() => ({}));
    setEnCours(false);
    if (reponse.ok) {
      toast.success(`Ingestion relancée : ${donnees.documentsRead} document(s) lu(s), ${donnees.chunksIndexed} chunk(s) indexé(s).`);
    } else {
      toast.error(donnees.erreur ?? `Échec (${reponse.status}).`);
    }
  }

  return (
    <div className="space-y-2">
      <Button size="sm" variant="outline" disabled={enCours} onClick={relancerIngestion}>
        <RefreshCw className={cn("h-4 w-4", enCours && "animate-spin")} aria-hidden />
        Relancer l&apos;ingestion
      </Button>
      <p className="text-xs text-muted-foreground">
        Relit les documents dans <code className="rounded bg-muted px-1 py-0.5">rag/corpus/</code> sur le serveur — n&apos;ajoute
        aucun nouveau fichier. Les documents doivent être bien structurés en Markdown (titres <code className="rounded bg-muted px-1 py-0.5">#</code>/<code className="rounded bg-muted px-1 py-0.5">##</code>/<code className="rounded bg-muted px-1 py-0.5">###</code>) :
        aucune validation automatique n&apos;existe, une mauvaise structure dégrade silencieusement la qualité des réponses.
        L&apos;opération est synchrone et peut prendre du temps.
      </p>
    </div>
  );
}
