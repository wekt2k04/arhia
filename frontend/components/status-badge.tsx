import { Badge } from "@/components/ui/badge";
import { Archive, CheckCircle2, Clock, Loader2, Lock, XCircle } from "lucide-react";
import { cn } from "@/lib/utils";

// WorkflowStatus côté Arhia.Domain/Enums.cs : InProgress=0, Closed=1, Archived=2, Cancelled=3, Suspended=4.
export const WORKFLOW_META = {
  0: { label: "En cours", cls: "bg-status-inprogress text-status-inprogress-foreground", Icon: Loader2 },
  1: { label: "Clôturé", cls: "bg-status-closed text-status-closed-foreground", Icon: Lock },
  2: { label: "Archivé", cls: "bg-status-archived text-status-archived-foreground", Icon: Archive },
  3: { label: "Annulé", cls: "bg-status-failed text-status-failed-foreground", Icon: XCircle },
  4: { label: "Suspendu", cls: "bg-status-pending text-status-pending-foreground", Icon: Clock },
} as const;

// ItemStatus : Pending=0, Done=1, Failed=2.
const ITEM_META = {
  0: { label: "En attente", cls: "bg-status-pending text-status-pending-foreground", Icon: Clock },
  1: { label: "Ok", cls: "bg-status-done text-status-done-foreground", Icon: CheckCircle2 },
  2: { label: "Ko", cls: "bg-status-failed text-status-failed-foreground", Icon: XCircle },
} as const;

export function WorkflowStatusBadge({ status }: { status: number }) {
  const meta = WORKFLOW_META[status as keyof typeof WORKFLOW_META];
  if (!meta) return <Badge variant="outline">Statut inconnu ({status})</Badge>;
  const { Icon } = meta;
  return (
    <Badge className={cn("gap-1 border-transparent", meta.cls)}>
      <Icon className="h-3 w-3" aria-hidden />
      {meta.label}
    </Badge>
  );
}

export function ItemStatusBadge({ status }: { status: number }) {
  const meta = ITEM_META[status as keyof typeof ITEM_META];
  if (!meta) return <Badge variant="outline">Statut inconnu ({status})</Badge>;
  const { Icon } = meta;
  return (
    <Badge className={cn("gap-1 border-transparent", meta.cls)}>
      <Icon className="h-3 w-3" aria-hidden />
      {meta.label}
    </Badge>
  );
}
