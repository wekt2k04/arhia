import { NextResponse } from 'next/server';

import { clearAuthCookie } from '@/lib/auth/cookies';

export const runtime = 'nodejs';

/**
 * Déconnexion : efface le cookie httpOnly `agirh_token` → 204 No Content.
 * Idempotent et sans effet de bord côté backend (le token meurt par expiration
 * côté serveur ; la révocation active reste hors périmètre de cette phase).
 */
export async function POST() {
  const response = new NextResponse(null, { status: 204 });
  clearAuthCookie(response);
  response.headers.set('Cache-Control', 'no-store');
  return response;
}
