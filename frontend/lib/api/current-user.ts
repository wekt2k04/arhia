import { AGIRH_API_URL } from "./config";
import { obtenirToken } from "./session";

export type UtilisateurCourant = {
  compteId: string;
  email: string;
  role: number;
  poleId: string | null;
};

// Réutilisé par la route BFF /api/auth/me (pour les composants client) et directement par les
// Server Components qui ont besoin de savoir qui est connecté (ex. app/chat/page.tsx).
export async function obtenirUtilisateurCourant(): Promise<UtilisateurCourant | null> {
  const token = await obtenirToken();
  if (!token) return null;

  const reponse = await fetch(`${AGIRH_API_URL}/api/auth/me`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (!reponse.ok) return null;

  const donnees = await reponse.json();
  return {
    compteId: donnees.compteId,
    email: donnees.email,
    role: donnees.role,
    poleId: donnees.poleId,
  };
}
