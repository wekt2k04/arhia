import { AlertTriangle, Inbox, Lock, SearchX } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import type { ReactNode } from "react";

type Kind = "empty" | "forbidden" | "not-found" | "error";

const META: Record<Kind, { Icon: typeof Inbox; defaultMessage: string }> = {
  empty: { Icon: Inbox, defaultMessage: "Rien à afficher ici pour le moment." },
  forbidden: { Icon: Lock, defaultMessage: "Vous n'avez pas accès à cette ressource." },
  "not-found": { Icon: SearchX, defaultMessage: "Introuvable." },
  error: { Icon: AlertTriangle, defaultMessage: "Une erreur est survenue." },
};

export function QueryState({ kind, message, action }: { kind: Kind; message?: string; action?: ReactNode }) {
  const { Icon, defaultMessage } = META[kind];
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-3 py-10 text-center text-muted-foreground">
        <Icon className="h-8 w-8" aria-hidden />
        <p>{message ?? defaultMessage}</p>
        {action}
      </CardContent>
    </Card>
  );
}
