import { NextRequest, NextResponse } from 'next/server';

import { getAuthCookie } from '@/lib/auth/cookies';

/**
 * Routes API publiques qui gèrent leur propre 401 :
 *  - /api/auth/login  → BFF de connexion (pose le cookie)
 *  - /api/auth/session → BFF de restauration de session (lit + valide exp)
 *  - /api/agent/*     → BFF agent (lit le cookie et relaie le token ; gère
 *                       ses propres 401 si le backend rejette le token)
 */
const PUBLIC_API_PREFIXES = ['/api/auth/login', '/api/auth/session', '/api/agent'];

function isPublicApi(pathname: string): boolean {
  return PUBLIC_API_PREFIXES.some(
    (prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`),
  );
}

/**
 * Première barrière (présence du cookie uniquement — l'expiration et la
 * signature sont vérifiées par /api/auth/session et par le backend .NET) :
 *  - Pages `/chat` : cookie absent → redirect `/login?returnUrl=...`
 *  - Routes `/api/*` privées : cookie absent → 401 JSON
 */
export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const token = getAuthCookie(request);

  const isChatPage = pathname === '/chat' || pathname.startsWith('/chat/');

  if (isChatPage && !token) {
    const returnUrl = pathname + request.nextUrl.search;
    const loginUrl = new URL('/login', request.url);
    loginUrl.searchParams.set('returnUrl', returnUrl);
    return NextResponse.redirect(loginUrl);
  }

  const isApiRoute = pathname === '/api' || pathname.startsWith('/api/');
  if (isApiRoute && !isPublicApi(pathname) && !token) {
    return NextResponse.json({ message: 'Non authentifié.' }, { status: 401 });
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/chat/:path*', '/api/:path*'],
};
