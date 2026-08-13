import type { NextRequest } from 'next/server';

import { forwardToBackend } from '@/lib/api/backendProxy';

/**
 * ─────────────────────────────────────────────────────────────────────────────
 *  BFF AGENT — proxy vers le backend .NET (http://localhost:5000)
 * ─────────────────────────────────────────────────────────────────────────────
 *  Forwarde TOUTE requête `/api/agent/*` (méthode, query, en-têtes, corps)
 *  vers `{AGIRH_API_URL}/api/agent/*` APRÈS injection du JWT lu dans le
 *  cookie httpOnly `agirh_token`.
 *
 *  La logique de proxy (auth cookie → Bearer, anti-buffering, SSE passthrough
 *  `duplex: 'half'`) est factorisée dans `src/lib/api/backendProxy.ts`
 *  (`forwardToBackend`) et partagée avec le proxy générique
 *  `src/app/api/[...path]/route.ts`. Ce fichier ne fait que brancher le
 *  préfixe `agent/` sur le helper.
 *
 *  Middleware (src/app/middleware.ts) : `/api/agent` figure dans
 *  PUBLIC_API_PREFIXES → la requête arrive jusqu'ici ; le 401 est produit ICI
 *  (cookie absent) ou propagé par le backend (token invalide/expiré).
 * ─────────────────────────────────────────────────────────────────────────────
 */

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, context: RouteContext): Promise<Response> {
  const { path } = await context.params;
  const pathname = Array.isArray(path) ? path.join('/') : String(path ?? '');
  return forwardToBackend(request, `agent/${pathname}`);
}

export { proxy as GET, proxy as POST, proxy as PUT, proxy as PATCH, proxy as DELETE };
