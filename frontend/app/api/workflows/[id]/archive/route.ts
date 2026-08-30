import { NextRequest, NextResponse } from "next/server";
import { ARHIA_API_URL } from "@/lib/api/config";
import { obtenirToken } from "@/lib/api/session";

export async function POST(_request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const token = await obtenirToken();
  if (!token) return NextResponse.json({ erreur: "Non authentifié." }, { status: 401 });

  const { id } = await params;

  const reponse = await fetch(`${ARHIA_API_URL}/api/workflows/${id}/archive`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!reponse.ok) {
    const texte = await reponse.text().catch(() => "");
    return NextResponse.json({ erreur: texte || "Échec de l'archivage." }, { status: reponse.status });
  }
  return new NextResponse(null, { status: 204 });
}
