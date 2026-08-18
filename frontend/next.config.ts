import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // La compression intégrée bufferise les réponses avant de les envoyer — incompatible avec le
  // streaming SSE (app/api/chat/ask, app/api/notifications/stream), vérifié empiriquement :
  // sans ça, rien n'arrivait au client avant la fin complète du flux côté Agirh.Api.
  compress: false,
  // Badge "N" de dev (bas gauche, next dev uniquement, jamais en prod) — désactivé, gênait les
  // captures d'écran/démos.
  devIndicators: false,
};

export default nextConfig;
