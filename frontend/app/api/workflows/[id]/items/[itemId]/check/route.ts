import { NextRequest, NextResponse } from "next/server";
import { ARHIA_API_URL } from "@/lib/api/config";
import { obtenirToken } from "@/lib/api/session";

export async function POST(request: NextRequest, { params }: { params: Promise<{ id: string; itemId: string }> }) {
  const token = await obtenirToken();
  if (!token) return NextResponse.json({ erreur: "Non authentifié." }, { status: 401 });

  const { id, itemId } = await params;
  const corps = await request.json();

  const reponse = await fetch(`${ARHIA_API_URL}/api/workflows/${id}/items/${itemId}/check`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify(corps),
  });

  if (!reponse.ok) {
    const texte = await reponse.text().catch(() => "");
    return NextResponse.json({ erreur: texte || "Échec du traitement de l'item." }, { status: reponse.status });
  }
  return new NextResponse(null, { status: 204 });
}
