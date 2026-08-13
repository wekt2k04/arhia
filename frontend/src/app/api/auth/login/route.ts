import { NextRequest, NextResponse } from 'next/server';

import { DEFAULT_MAX_AGE_SECONDS, setAuthCookie } from '@/lib/auth/cookies';
import { decodeJwtPayload, getExpiryFromPayload } from '@/lib/auth/jwt';

export const runtime = 'nodejs';

/** URL du backend .NET — résolue côté serveur uniquement (jamais NEXT_PUBLIC_). */
const AGIRH_API_URL = (process.env.AGIRH_API_URL ?? 'http://localhost:5000').replace(/\/+$/, '');

/**
 * BFF de connexion : proxy vers `POST {AGIRH_API_URL}/api/auth/login`.
 *
 *  - Succès  : pose le cookie httpOnly `agirh_token` (Max-Age aligné sur `exp`)
 *              et renvoie `{ user }` — LE TOKEN N'EST JAMAIS RENVOYÉ AU CLIENT.
 *  - Échec   : statut normalisé (401 générique / 429 distinct / 503 indispo),
 *              jamais le corps backend brut, jamais `ex.Message`.
 *
 * Le backend .NET (AuthController.cs) NE pose PAS de cookie : la conversion
 * « token-body → cookie httpOnly » est faite ici, dans le BFF.
 */
export async function POST(request: NextRequest) {
  const noStoreHeaders = { 'Cache-Control': 'no-store' };

  // ── 1. Lecture + validation minimale du corps (jamais de données techniques en erreur)
  let email = '';
  let password = '';
  try {
    const body = (await request.json()) as { email?: unknown; password?: unknown };
    email = typeof body?.email === 'string' ? body.email.trim().toLowerCase() : '';
    password = typeof body?.password === 'string' ? body.password : '';
  } catch {
    return NextResponse.json({ message: 'Email ou mot de passe invalide.' }, { status: 401, headers: noStoreHeaders });
  }

  if (!email || !password) {
    return NextResponse.json({ message: 'Email ou mot de passe invalide.' }, { status: 401, headers: noStoreHeaders });
  }

  // ── 2. Appel backend (timeout borné, pas de redirection de credentials)
  let backendResponse: Response;
  try {
    backendResponse = await fetch(`${AGIRH_API_URL}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ email, password }),
      cache: 'no-store',
      redirect: 'error',
      signal: AbortSignal.timeout(10_000),
    });
  } catch {
    return NextResponse.json(
      { message: 'Service temporairement indisponible, réessayez plus tard.' },
      { status: 503, headers: noStoreHeaders },
    );
  }

  // ── 3. Normalisation des échecs : jamais le corps backend, jamais ex.Message
  if (backendResponse.status === 429) {
    return NextResponse.json(
      { message: 'Trop de tentatives de connexion, réessayez plus tard.' },
      { status: 429, headers: noStoreHeaders },
    );
  }

  if (backendResponse.status >= 500) {
    return NextResponse.json(
      { message: 'Service temporairement indisponible, réessayez plus tard.' },
      { status: 503, headers: noStoreHeaders },
    );
  }

  if (!backendResponse.ok) {
    // 400/401 (identifiants invalides, compte inactif…) → message générique unique
    return NextResponse.json({ message: 'Email ou mot de passe invalide.' }, { status: 401, headers: noStoreHeaders });
  }

  // ── 4. Réponse backend : `{ token, employeeId, role, firstName, lastName }`
  //    (plat, camelCase — sérialisation .NET par défaut, confirmée par tests.http)
  let data: unknown;
  try {
    data = await backendResponse.json();
  } catch {
    return NextResponse.json(
      { message: 'Service temporairement indisponible, réessayez plus tard.' },
      { status: 502, headers: noStoreHeaders },
    );
  }

  const rec = (data ?? {}) as Record<string, unknown>;
  const token = typeof rec['token'] === 'string' ? rec['token'] : '';
  const id = typeof rec['employeeId'] === 'string' ? rec['employeeId'] : '';
  const role = typeof rec['role'] === 'string' ? rec['role'] : '';
  const firstName = typeof rec['firstName'] === 'string' ? rec['firstName'] : '';
  const lastName = typeof rec['lastName'] === 'string' ? rec['lastName'] : '';

  if (!token || !id || !role) {
    // Réponse backend inexploitable → on refuse, sans détail technique
    return NextResponse.json({ message: 'Email ou mot de passe invalide.' }, { status: 401, headers: noStoreHeaders });
  }

  // ── 5. Alignement Max-Age du cookie sur l'expiration réelle du JWT
  const payload = decodeJwtPayload(token);
  const expiresAt = payload ? getExpiryFromPayload(payload) : null;
  const maxAge =
    expiresAt !== null && Number.isFinite(expiresAt)
      ? Math.max(1, expiresAt - Math.floor(Date.now() / 1000))
      : DEFAULT_MAX_AGE_SECONDS;

  // ── 6. Pose du cookie httpOnly + retour `{ user }` (JAMAIS le token)
  const response = NextResponse.json(
    { user: { id, email, firstName, lastName, role } },
    { status: 200, headers: noStoreHeaders },
  );
  setAuthCookie(response, token, maxAge);

  return response;
}
