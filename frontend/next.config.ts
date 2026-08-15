import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // La compression intégrée bufferise les réponses avant de les envoyer — incompatible avec le
  // streaming SSE (app/api/chat/demander, app/api/notifications/stream), vérifié empiriquement :
  // sans ça, rien n'arrivait au client avant la fin complète du flux côté Agirh.Api.
  compress: false,
};

export default nextConfig;
