import { UsersRound } from "lucide-react";
import { cn } from "@/lib/utils";

// Placeholder pour le logo/l'image AGIRH — remplacer par une <Image> une fois l'asset fourni
// par le porteur du projet. Le reste de l'app consomme <AgirhLogo>/<AgirhMark>, jamais cette
// forme directement, pour que le remplacement se fasse à un seul endroit.
export function AgirhMark({ className }: { className?: string }) {
  return (
    <span
      className={cn(
        "flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary text-primary-foreground",
        className,
      )}
    >
      <UsersRound className="h-4 w-4" aria-hidden />
    </span>
  );
}

export function AgirhLogo({ className, iconOnly = false }: { className?: string; iconOnly?: boolean }) {
  return (
    <span className={cn("inline-flex items-center gap-2", className)}>
      <AgirhMark />
      {!iconOnly && (
        <span className="text-base font-bold tracking-tight text-foreground">AGIRH</span>
      )}
    </span>
  );
}
