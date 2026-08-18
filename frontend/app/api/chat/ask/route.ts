import { NextRequest } from "next/server";
import { AGIRH_API_URL } from "@/lib/api/config";
import { obtenirToken } from "@/lib/api/session";

// Jamais mis en cache/optimisé statiquement : c'est un flux SSE, par nature différent à chaque
// requête (docs/STACK_TECHNIQUE.md #1).
export const dynamic = "force-dynamic";

// Proxy BFF pur : relit le cookie de session, ajoute le Bearer token, et relaie tel quel le flux
// SSE de Agirh.Api.ChatController (event: fragment / event: done) — pas de reparsing des
// frames ici, response.body est déjà un ReadableStream, on le transmet directement.
export async function GET(request: NextRequest) {
  const token = await obtenirToken();
  if (!token) {
    return new Response("Non authentifié.", { status: 401 });
  }

  const { searchParams } = new URL(request.url);
  const apiUrl = new URL(`${AGIRH_API_URL}/api/chat/ask`);
  apiUrl.searchParams.set("question", searchParams.get("question") ?? "");
  const targetEmployeeId = searchParams.get("targetEmployeeId");
  if (targetEmployeeId) {
    apiUrl.searchParams.set("targetEmployeeId", targetEmployeeId);
  }

  const reponseApi = await fetch(apiUrl, {
    headers: { Authorization: `Bearer ${token}` },
    signal: request.signal,
  });

  if (!reponseApi.ok || !reponseApi.body) {
    return new Response("Erreur lors de la communication avec l'assistant.", { status: 502 });
  }

  return new Response(reponseApi.body, {
    status: 200,
    headers: {
      "Content-Type": "text/event-stream",
      "Cache-Control": "no-cache, no-transform",
      Connection: "keep-alive",
    },
  });
}
