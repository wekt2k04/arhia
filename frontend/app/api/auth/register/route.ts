import { NextRequest, NextResponse } from "next/server";
import { AGIRH_API_URL } from "@/lib/api/config";
import { definirSession } from "@/lib/api/session";

// Auto-inscription -> rôle Collaborateur par défaut, jamais élevé à l'inscription
// (LOGIQUE_METIER.md §1) — décidé et appliqué côté Agirh.Api, pas ici.
export async function POST(request: NextRequest) {
  const corps = await request.json();

  const reponse = await fetch(`${AGIRH_API_URL}/api/auth/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(corps),
  });

  if (!reponse.ok) {
    const texte = await reponse.text();
    return NextResponse.json({ erreur: texte || "Échec de l'inscription." }, { status: reponse.status });
  }

  const donnees = await reponse.json();
  await definirSession(donnees.token);

  return NextResponse.json({
    compteId: donnees.compteId,
    email: donnees.email,
    role: donnees.role,
    poleId: donnees.poleId,
  });
}
