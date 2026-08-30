import { NextRequest, NextResponse } from "next/server";
import { ARHIA_API_URL } from "@/lib/api/config";
import { obtenirToken } from "@/lib/api/session";

export async function POST(request: NextRequest) {
  const token = await obtenirToken();
  if (!token) return NextResponse.json({ erreur: "Non authentifié." }, { status: 401 });

  const corps = await request.json();

  const reponse = await fetch(`${ARHIA_API_URL}/api/employees`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify(corps),
  });

  const donnees = await reponse.json().catch(() => null);
  if (!reponse.ok) {
    return NextResponse.json({ erreur: (donnees && donnees.title) || donnees || "Échec de la création." }, { status: reponse.status });
  }
  return NextResponse.json(donnees, { status: 200 });
}
