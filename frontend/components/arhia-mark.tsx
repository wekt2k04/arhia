import Image from "next/image";
import { cn } from "@/lib/utils";

// Logo produit arhia (public/arhia-logo.png, genere par script Python/Pillow — hexagone en
// clin d'oeil a l'architecture hexagonale reelle du projet + mot-marque, dégradé indigo cohérent
// avec l'identité --primary déjà établie). Dimensions intrinsèques 1280x360 (ratio ~3.56),
// respectées via width/height explicites ; la taille affichée se règle uniquement par la classe
// de hauteur (h-*), jamais par un stretch qui déformerait le logo.
export function ArhiaLogo({ className }: { className?: string }) {
  return (
    <Image
      src="/arhia-logo.png"
      alt="arhia"
      width={1280}
      height={360}
      priority
      className={cn("h-9 w-auto", className)}
    />
  );
}
