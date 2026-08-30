import { NextResponse } from "next/server";
import { ARHIA_API_URL } from "@/lib/api/config";
import { obtenirToken } from "@/lib/api/session";

// Proxy BFF pur (patron auth/login/route.ts) : relit le cookie de session, ajoute le Bearer
// token, relaie le vrai status code. L'endpoint réel ne prend aucun corps de requête — il relit
// un dossier fixe côté serveur (Program.cs, CorpusOptions).
export async function POST() {
  const token = await obtenirToken();
  if (!token) {
    return NextResponse.json({ erreur: "Non authentifié." }, { status: 401 });
  }

  const reponse = await fetch(`${ARHIA_API_URL}/api/admin/reindex-corpus`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!reponse.ok) {
    const texte = await reponse.text();
    return NextResponse.json({ erreur: texte || "Échec de la réindexation." }, { status: reponse.status });
  }

  const donnees = await reponse.json();
  return NextResponse.json(donnees);
}
