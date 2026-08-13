/**
 * Client HTTP NAVIGATEUR vers le BFF AGIRH.
 * ---------------------------------------------------------------------------
 * À utiliser UNIQUEMENT côté client (client components, hooks) — jamais dans
 * un Server Component ou un route handler.
 *
 *  - Requêtes same-origin vers `/api/*` : le cookie httpOnly `agirh_token`
 *    est transmis automatiquement par le navigateur (aucune gestion de token,
 *    aucune fuite).
 *  - 401 → redirection immédiate vers `/login?returnUrl=...` (AVANT de lever
 *    toute erreur) : session absente ou expirée.
 *  - Statut non-ok → lève `ApiError` avec UN MESSAGE UTILISATEUR GÉNÉRIQUE.
 *    On ne propage JAMAIS de détail technique (corps backend, stack, headers).
 * ---------------------------------------------------------------------------
 */

/** Erreur API navigable côté client : statut HTTP + message utilisateur générique. */
export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

const GENERIC_STATUS_MESSAGES: Record<number, string> = {
  403: 'Accès refusé.',
  404: 'Élément introuvable.',
  409: 'Conflit avec l’état actuel des données.',
  429: 'Trop de requêtes. Réessayez dans un instant.',
};

function genericMessageForStatus(status: number): string {
  if (GENERIC_STATUS_MESSAGES[status]) return GENERIC_STATUS_MESSAGES[status];
  if (status >= 500) return 'Service temporairement indisponible. Réessayez plus tard.';
  return 'Une erreur est survenue. Réessayez plus tard.';
}

/**
 * Appel JSON typé vers le BFF same-origin.
 *  - 204 No Content → résout `undefined`.
 *  - Statut non-2xx → lève `ApiError` (message générique, jamais technique).
 *  - Corps non-JSON → renvoyé tel quel (texte) dans la réponse typée.
 */
export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, { ...init, cache: 'no-store' });
  } catch {
    throw new ApiError(0, 'Connexion impossible. Vérifiez votre réseau puis réessayez.');
  }

  // Session absente/expirée : retour à la connexion AVANT toute erreur levée.
  if (response.status === 401) {
    const returnUrl = encodeURIComponent(window.location.pathname);
    window.location.replace(`/login?returnUrl=${returnUrl}`);
    throw new ApiError(401, 'Votre session a expiré. Veuillez vous reconnecter.');
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  let parsed: unknown = null;
  if (text) {
    try {
      parsed = JSON.parse(text);
    } catch {
      parsed = text;
    }
  }

  if (!response.ok) {
    throw new ApiError(response.status, genericMessageForStatus(response.status));
  }

  return parsed as T;
}
