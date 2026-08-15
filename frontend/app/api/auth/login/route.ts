import { NextRequest, NextResponse } from "next/server";
import { AGIRH_API_URL } from "@/lib/api/config";
import { definirSession } from "@/lib/api/session";

// BFF (STACK_TECHNIQUE.md #2) : le navigateur appelle cette route, jamais l'Api .NET
// directement. Le token JWT reçu de l'Api est posé en cookie httpOnly ici et jamais renvoyé
// dans le corps de la réponse — le client ne le voit jamais en clair.
export async function POST(request: NextRequest) {
  const corps = await request.json();

  const reponse = await fetch(`${AGIRH_API_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(corps),
  });

  if (!reponse.ok) {
    const texte = await reponse.text();
    return NextResponse.json({ erreur: texte || "Échec de connexion." }, { status: reponse.status });
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
