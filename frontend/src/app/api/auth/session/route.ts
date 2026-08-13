import { NextRequest, NextResponse } from 'next/server';

import { getAuthCookie } from '@/lib/auth/cookies';
import { decodeJwtPayload, extractAuthUser, getExpiryFromPayload } from '@/lib/auth/jwt';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

const UNAUTHORIZED_HEADERS = { 'Cache-Control': 'no-store' };

/**
 * Restaure la session : lit le cookie httpOnly, décode la payload JWT
 * (base64url, SANS vérifier la signature — voir lib/auth/jwt.ts) et vérifie
 * l'expiration (`exp`).
 *
 *  - Cookie absent / malformé / expiré → 401.
 *  - Sinon → `{ user: { id, email, firstName, lastName, role } }`.
 *
 *  LE TOKEN N'EST JAMAIS RENVOYÉ.
 *  Rappel : la validation cryptographique réelle reste au backend .NET ;
 *  cette route ne fait qu'alimenter l'UX (et ne révèle aucune donnée sensible).
 */
export async function GET(request: NextRequest) {
  const token = getAuthCookie(request);

  if (!token) {
    return NextResponse.json({ message: 'Non authentifié.' }, { status: 401, headers: UNAUTHORIZED_HEADERS });
  }

  const payload = decodeJwtPayload(token);
  if (!payload) {
    return NextResponse.json({ message: 'Session invalide.' }, { status: 401, headers: UNAUTHORIZED_HEADERS });
  }

  const exp = getExpiryFromPayload(payload);
  const now = Math.floor(Date.now() / 1000);
  // Tolérance d'horloge de 30 s (le backend impose la vraie durée avec 1 min de skew).
  if (exp === null || exp <= now - 30) {
    return NextResponse.json({ message: 'Session expirée.' }, { status: 401, headers: UNAUTHORIZED_HEADERS });
  }

  const user = extractAuthUser(payload);
  if (!user) {
    return NextResponse.json({ message: 'Session invalide.' }, { status: 401, headers: UNAUTHORIZED_HEADERS });
  }

  return NextResponse.json({ user }, { status: 200, headers: UNAUTHORIZED_HEADERS });
}
