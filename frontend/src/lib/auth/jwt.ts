/**
 * Helpers de décodage de payload JWT — RÉSERVÉS AUX ROUTE HANDLERS SERVEUR.
 * ----------------------------------------------------------------------------
 * IMPORTANT (sécurité) :
 *  - Ces helpers NE VÉRIFIENT PAS la signature : ils servent uniquement à
 *    extraire l'identité et l'expiration pour l'UX (session route) et à
 *    aligner le Max-Age du cookie (login route).
 *  - La VÉRIFICATION réelle de signature, d'issuer, d'audience et de durée
 *    reste la responsabilité du backend .NET (JwtBearer). Un cookie forgé
 *    sera donc rejeté par le backend, même si la session route le lit.
 *  - Aucune clé de signature ne transite dans ce module, ni ne doit y être
 *    ajoutée : jamais de secret côté frontend.
 * ----------------------------------------------------------------------------
 * Claims émis par JwtTokenService.cs (mapping outbound .NET) :
 *   nameid (id), email, given_name, family_name, role, is_active, managerId, exp
 * Des URIs longues (schemas.xmlsoap.org / schemas.microsoft.com) sont
 * également gérées en fallback par précaution.
 */

export interface JwtPayload {
  [key: string]: unknown;
}

export interface AuthUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
}

/**
 * Décode le segment payload d'un JWT (base64url → UTF-8 → JSON).
 * Retourne `null` si le token est malformé ou le payload non-JSON.
 * Ne vérifie JAMAIS la signature (volontairement — voir en-tête).
 */
export function decodeJwtPayload(token: string): JwtPayload | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;

    const payloadSegment = parts[1];
    if (!payloadSegment) return null;

    const json = Buffer.from(payloadSegment, 'base64url').toString('utf8');
    const parsed: unknown = JSON.parse(json);

    if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) return null;

    return parsed as JwtPayload;
  } catch {
    return null;
  }
}

/**
 * Extrait l'expiration (`exp`) du payload.
 * `exp` est émis comme timestamp Unix (secondes) par le backend.
 * Retourne `null` si absent ou non numérique.
 */
export function getExpiryFromPayload(payload: JwtPayload): number | null {
  const exp = payload['exp'];

  if (typeof exp === 'number' && Number.isFinite(exp)) return exp;

  if (typeof exp === 'string' && exp.trim() !== '') {
    const parsed = Number(exp);
    if (Number.isFinite(parsed)) return parsed;
  }

  return null;
}

/**
 * Construit l'objet `user` à partir des claims du payload.
 * Champs requis : id, email, role. firstName/lastName peuvent être vides.
 * Retourne `null` si l'identité minimale est absente (session inexploitable).
 */
export function extractAuthUser(payload: JwtPayload): AuthUser | null {
  const asString = (value: unknown): string =>
    typeof value === 'string' ? value : typeof value === 'number' ? String(value) : '';

  const id =
    asString(payload['nameid']) ||
    asString(payload['sub']) ||
    asString(payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']);

  const email =
    asString(payload['email']) ||
    asString(payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress']);

  const firstName =
    asString(payload['given_name']) ||
    asString(payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname']);

  const lastName =
    asString(payload['family_name']) ||
    asString(payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname']);

  const role =
    asString(payload['role']) ||
    asString(payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']);

  if (!id || !email || !role) return null;

  return { id, email, firstName, lastName, role };
}
