import type { NextRequest } from 'next/server';

import { forwardToBackend } from '@/lib/api/backendProxy';

/**
 * ─────────────────────────────────────────────────────────────────────────────
 *  BFF GÉNÉRIQUE — proxy vers le backend .NET (http://localhost:5000)
 * ─────────────────────────────────────────────────────────────────────────────
 *  Catch-all `/api/*` : forwarde TOUTE requête non capturée par une route plus
 *  spécifique (méthode, query, en-têtes, corps) vers `{AGIRH_API_URL}/api/*`
 *  APRÈS injection du JWT lu dans le cookie httpOnly `agirh_token`.
 *
 *  Routes qui GAGNENT par précédence App Router (ne passent PAS ici) :
 *   - `/api/auth/login`, `/api/auth/session`, `/api/auth/logout` (BFF auth)
 *   - `/api/agent/*` (BFF agent — chat SSE, `agent/[...path]`)
 *
 *  La logique de proxy (auth cookie → Bearer, anti-buffering, SSE passthrough
 *  `duplex: 'half'`) est factorisée dans `src/lib/api/backendProxy.ts`
 *  (`forwardToBackend`), partagée avec `api/agent/[...path]/route.ts`.
 *
 *  Middleware (src/app/middleware.ts) : `/api/*` hors prefixes publics exige
 *  le cookie → sans session, 401 JSON produit par le middleware (comportement
 *  accepté) ; avec session, la requête passe ici et le helper relaie le Bearer.
 * ─────────────────────────────────────────────────────────────────────────────
 */

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, context: RouteContext): Promise<Response> {
  const { path } = await context.params;
  const pathname = Array.isArray(path) ? path.join('/') : String(path ?? '');
  return forwardToBackend(request, pathname);
}

export { proxy as GET, proxy as POST, proxy as PUT, proxy as PATCH, proxy as DELETE };
