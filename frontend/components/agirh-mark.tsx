import Image from "next/image";
import { cn } from "@/lib/utils";

// Logo reel AGIRH (public/agirh-logo.png, fourni par le porteur du projet — fond transparent,
// lisible aussi bien en clair qu'en sombre). Dimensions intrinseques 126x64 (ratio ~1.97)
// respectees via width/height explicites ; la taille affichee se regle uniquement par la classe
// de hauteur (h-*), jamais par un stretch qui deformerait le logo. Seul point de remplacement de
// l'app : tout le reste consomme <AgirhLogo>, jamais ce fichier directement.
export function AgirhLogo({ className }: { className?: string }) {
  return (
    <Image
      src="/agirh-logo.png"
      alt="AGIRH — Gestion Intégrée Du Capital Humain"
      width={126}
      height={64}
      priority
      className={cn("h-9 w-auto", className)}
    />
  );
}
