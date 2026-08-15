import { NextResponse } from "next/server";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";

// Consommé par les composants client pour savoir qui est connecté, sans jamais exposer le JWT.
export async function GET() {
  const utilisateur = await obtenirUtilisateurCourant();
  if (!utilisateur) {
    return NextResponse.json({ authentifie: false });
  }

  return NextResponse.json({ authentifie: true, ...utilisateur });
}
