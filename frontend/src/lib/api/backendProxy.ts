/**
 * ─────────────────────────────────────────────────────────────────────────────
 *  BFF BACKEND — proxy commun vers le backend .NET (http://localhost:5000)
 * ─────────────────────────────────────────────────────────────────────────────
 *  Factorisation de la logique du proxy agent d'origine
 *  (`src/app/api/agent/[...path]/route.ts`, Phase 0) en un seul helper :
 *
 *      forwardToBackend(request, pathname)
 *
 *  Forwarde UNE requête NextRequest vers `{AGIRH_API_URL}/api/{pathname}`
 *  (méthode, query, en-têtes, corps) APRÈS injection du JWT lu dans le cookie
 *  httpOnly `agirh_token` (cf. src/lib/auth/cookies.ts — contrat @secops).
 *
 *  CONTRAT d'utilisation :
 *   - `pathname` = chemin SOUS `/api/` SANS le préfixe `/api/` ni slash final
 *     (ex. `agent/chat`, `conversations`, `conversations/{id}/messages`).
 *   - La query de la requête entrante est préservée telle quelle.
 *
 *  Consommateurs :
 *   - `src/app/api/agent/[...path]/route.ts` — proxy agent (chat SSE compris)
 *   - `src/app/api/[...path]/route.ts` — proxy générique (conversations,
 *     salary-advance, leave…). Les routes spécifiques (`api/auth/*`,
 *     `api/agent/*`) gagnent par précédence App Router et ne passent PAS ici.
 *
 *  Chat SSE : ne JAMAIS bufferiser le flux. Le corps upstream (ReadableStream)
 *  est re-streamé tel quel dans `new Response(stream, ...)` avec les en-têtes
 *  de streaming. `duplex: 'half'` est requis par undici quand le corps est un
 *  flux (sinon `fetch` lève pour le chat).
 * ─────────────────────────────────────────────────────────────────────────────
 */

import type { NextRequest } from 'next/server';

import { getAuthCookie } from '@/lib/auth/cookies';

/** URL du backend .NET — résolue côté serveur uniquement (jamais NEXT_PUBLIC_). */
const API_BASE = (process.env.AGIRH_API_URL ?? 'http://localhost:5000').replace(/\/+$/, '');

// En-têtes « hop-by-hop » + host : gérés par les proxies, jamais relayés.
const HOP_BY_HOP_HEADERS = new Set([
  'connection',
  'keep-alive',
  'proxy-authenticate',
  'proxy-authorization',
  'te',
  'trailer',
  'transfer-encoding',
  'upgrade',
  'content-length',
  'host',
]);

/**
 * Forwarde `request` vers le backend AGIRH sous `/api/{pathname}`.
 * Retourne une `Response` prête pour le route handler (statut, en-têtes,
 * corps streamé — jamais bufferisé).
 */
export async function forwardToBackend(request: NextRequest, pathname: string): Promise<Response> {
  // 1. Authentification : le JWT ne vit que dans le cookie httpOnly.
  const token = getAuthCookie(request);
  if (!token) {
    return Response.json(
      { message: 'Non authentifié.' },
      { status: 401, headers: { 'Cache-Control': 'no-store' } },
    );
  }

  // 2. Cible backend (query préservée).
  const target = `${API_BASE}/api/${pathname}${request.nextUrl.search}`;

  // 3. En-têtes : on relaie tout SAUF hop-by-hop/host, puis on injecte Bearer.
  const headers = new Headers();
  request.headers.forEach((value, key) => {
    if (HOP_BY_HOP_HEADERS.has(key.toLowerCase())) return;
    headers.set(key, value);
  });
  headers.set('Authorization', `Bearer ${token}`);

  // 4. Corps : pour les méthodes avec corps, on passe le flux brut.
  //    `duplex: 'half'` requis par undici/fetch Node quand le corps est un
  //    ReadableStream — sans lui, fetch lève une erreur (chat SSE).
  const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
  const init: RequestInit & { duplex?: 'half' } = {
    method: request.method,
    headers,
    redirect: 'manual',
  };
  if (hasBody && request.body) {
    init.body = request.body;
    init.duplex = 'half';
  }

  // 5. Forward vers le backend.
  let upstream: Response;
  try {
    upstream = await fetch(target, init);
  } catch {
    return Response.json(
      { message: 'Service temporairement indisponible.' },
      { status: 502, headers: { 'Cache-Control': 'no-store' } },
    );
  }

  // 6. Réponse : copie du statut + en-têtes non hop-by-hop.
  const responseHeaders = new Headers();
  upstream.headers.forEach((value, key) => {
    if (HOP_BY_HOP_HEADERS.has(key.toLowerCase())) return;
    responseHeaders.set(key, value);
  });

  // 6bis. SSE : garantit les en-têtes de streaming (chat notamment).
  const contentType = upstream.headers.get('content-type') ?? '';
  if (contentType.includes('text/event-stream')) {
    responseHeaders.set('Content-Type', 'text/event-stream');
    responseHeaders.set('Cache-Control', 'no-cache, no-transform');
    responseHeaders.set('X-Accel-Buffering', 'no');
    responseHeaders.set('Connection', 'keep-alive');
  }

  // 7. JAMAIS de buffering : le corps upstream est re-streamé tel quel.
  if (upstream.body) {
    return new Response(upstream.body, { status: upstream.status, headers: responseHeaders });
  }
  return new Response(null, { status: upstream.status, headers: responseHeaders });
}
