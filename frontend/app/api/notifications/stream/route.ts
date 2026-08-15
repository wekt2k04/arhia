import { NextRequest } from "next/server";
import { AGIRH_API_URL } from "@/lib/api/config";
import { obtenirToken } from "@/lib/api/session";

export const dynamic = "force-dynamic";

// Même proxy SSE pur que app/api/chat/demander — voir ce fichier pour le détail. Ici la source
// est Agirh.Api.NotificationController (SseNotificationBroadcaster, rafraîchi toutes les 10s).
export async function GET(request: NextRequest) {
  const token = await obtenirToken();
  if (!token) {
    return new Response("Non authentifié.", { status: 401 });
  }

  const reponseApi = await fetch(`${AGIRH_API_URL}/api/notifications/stream`, {
    headers: { Authorization: `Bearer ${token}` },
    signal: request.signal,
  });

  if (!reponseApi.ok || !reponseApi.body) {
    return new Response("Erreur lors de la communication avec les notifications.", { status: 502 });
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
