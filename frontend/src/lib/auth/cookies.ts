/**
 * ─────────────────────────────────────────────────────────────────────────────
 *  CONTRAT COMMUN — Gestion du cookie de session AGIRH (Next.js App Router)
 * ─────────────────────────────────────────────────────────────────────────────
 *  Architecture « JWT hors risque XSS » :
 *   - Le token JWT n'est JAMAIS exposé au JavaScript client.
 *   - Il est stocké UNIQUEMENT dans un cookie `httpOnly` posé par les route
 *     handlers BFF (côté serveur) — jamais par le navigateur, jamais dans
 *     localStorage / sessionStorage.
 *   - Les routes `/api/agent/*` (BFF agent) DOIVENT lire le cookie via
 *     `getAuthCookie(request)` puis relayer le token dans l'en-tête
 *     `Authorization: Bearer <token>` vers le backend .NET.
 *
 *  Contrat exporté (fonctionne dans les route handlers ET le middleware) :
 *   - getAuthCookie(request)                     → string | null
 *   - setAuthCookie(response, token, maxAgeSeconds?)
 *   - clearAuthCookie(response)
 *
 *  NE JAMAIS :
 *   - renvoyer le token dans une réponse JSON ;
 *   - stocker le token dans localStorage / sessionStorage ;
 *   - tenter de lire ce cookie côté client (httpOnly l'interdit de toute façon).
 *
 *  Alignement backend (vérifié dans AuthController.cs / JwtTokenService.cs) :
 *   - Durée JWT : `Jwt:ExpiryHours`, défaut 8 h → 28 800 s (borné 1..24 h).
 * ─────────────────────────────────────────────────────────────────────────────
 */

/** Nom du cookie de session. */
export const AUTH_COOKIE_NAME = 'agirh_token';

/**
 * Durée de vie par défaut du cookie, alignée sur l'expiration JWT du backend
 * (JwtTokenService.cs : `Jwt:ExpiryHours`, défaut 8 h → 28 800 s).
 */
export const DEFAULT_MAX_AGE_SECONDS = 8 * 60 * 60; // 28800

/**
 * Lit la valeur brute du cookie `agirh_token` depuis une requête.
 * Retourne `null` si absent (le middleware s'appuie sur cette présence).
 * Ne décode PAS le JWT : la vérification d'expiration appartient à la route
 * `/api/auth/session` (et la vérification de signature au backend .NET).
 */
export function getAuthCookie(request: Request): string | null {
  const cookieHeader = request.headers.get('cookie');
  if (!cookieHeader) return null;

  for (const part of cookieHeader.split(';')) {
    const separatorIndex = part.indexOf('=');
    if (separatorIndex < 0) continue;

    const name = part.slice(0, separatorIndex).trim();
    if (name !== AUTH_COOKIE_NAME) continue;

    const value = part.slice(separatorIndex + 1).trim();
    if (value === '') return '';

    try {
      return decodeURIComponent(value);
    } catch {
      // Cookie malformé : on le renvoie tel quel — l'échec de validation
      // du payload JWT dans /api/auth/session retombera proprement en 401.
      return value;
    }
  }

  return null;
}

/**
 * Pose le cookie de session sur une réponse HTTP.
 * Drapeaux imposés : `HttpOnly`, `Path=/`, `SameSite=Lax`,
 * `Secure` en production, `Max-Age` aligné sur l'expiration JWT
 * (défaut 8 h = 28 800 s, paramétré par la route de login à partir d'`exp`).
 */
export function setAuthCookie(
  response: Response,
  token: string,
  maxAgeSeconds: number = DEFAULT_MAX_AGE_SECONDS,
): void {
  const secure = process.env.NODE_ENV === 'production';
  const maxAge = Math.max(1, Math.floor(Number(maxAgeSeconds) || DEFAULT_MAX_AGE_SECONDS));

  const attributes = [
    `${AUTH_COOKIE_NAME}=${encodeURIComponent(token)}`,
    'HttpOnly',
    'Path=/',
    `Max-Age=${maxAge}`,
    'SameSite=Lax',
  ];
  if (secure) attributes.push('Secure');

  response.headers.set('Set-Cookie', attributes.join('; '));
}

/**
 * Supprime le cookie de session (mêmes drapeaux que la pose, pour garantir
 * l'effacement y compris en production avec `Secure`).
 */
export function clearAuthCookie(response: Response): void {
  const secure = process.env.NODE_ENV === 'production';

  const attributes = [
    `${AUTH_COOKIE_NAME}=`,
    'HttpOnly',
    'Path=/',
    'Max-Age=0',
    'Expires=Thu, 01 Jan 1970 00:00:00 GMT',
    'SameSite=Lax',
  ];
  if (secure) attributes.push('Secure');

  response.headers.set('Set-Cookie', attributes.join('; '));
}
