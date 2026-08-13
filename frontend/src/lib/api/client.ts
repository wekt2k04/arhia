/**
 * Client HTTP SERVEUR vers le backend AGIRH (.NET — http://localhost:5000).
 * ---------------------------------------------------------------------------
 * À utiliser UNIQUEMENT côté serveur (Server Components, Route Handlers,
 * Server Actions). Ne JAMAIS l'importer depuis un composant client : la base
 * URL n'est pas préfixée `NEXT_PUBLIC_` et ne doit pas fuiter dans le bundle.
 *
 * Contrats wire format : src/contracts/*.ts (camelCase — vérifié tests.http).
 * Propagation des statuts : toute réponse non-2xx lève `ApiError` avec le
 * `status` HTTP préservé (401/403/404/429/5xx) et le corps déjà lu.
 *
 * Le streaming SSE ne passe PAS par ce client : il est géré par le proxy BFF
 * `src/app/api/agent/[...path]/route.ts` (flux jamais bufferisé).
 * ---------------------------------------------------------------------------
 */

import type { ApiErrorBody } from '@/contracts';

const DEFAULT_API_BASE = 'http://localhost:5000';

export function getApiBaseUrl(): string {
  return (process.env.AGIRH_API_URL ?? DEFAULT_API_BASE).replace(/\/+$/, '');
}

export class ApiError extends Error {
  readonly status: number;
  readonly statusText: string;
  readonly body: unknown;

  constructor(status: number, statusText: string, body: unknown) {
    super(`AGIRH API ${status} ${statusText}`);
    this.name = 'ApiError';
    this.status = status;
    this.statusText = statusText;
    this.body = body;
  }

  /** Message utilisateur si le backend en a fourni un (`{ message }`). */
  get userMessage(): string | null {
    if (this.body !== null && typeof this.body === 'object' && !Array.isArray(this.body)) {
      const candidate = (this.body as ApiErrorBody).message;
      if (typeof candidate === 'string' && candidate.trim() !== '') return candidate;
    }
    return null;
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

export interface ApiFetchOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  headers?: HeadersInit;
  /** Sérialisé en JSON (application/json). */
  body?: unknown;
  /** JWT pour les appels serveur-à-serveur (jamais depuis un client navigateur). */
  token?: string;
  timeoutMs?: number;
  signal?: AbortSignal;
}

/**
 * Appel JSON typé vers le backend AGIRH.
 *  - 204 No Content → résout `undefined`.
 *  - Statut non-2xx → lève `ApiError` (status + corps lu).
 *  - Corps non-JSON → renvoyé tel quel (texte) dans la réponse typée.
 */
export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  const { method = 'GET', headers, body, token, timeoutMs = 30_000, signal } = options;

  // Timeout borné + signal externe combinés via un AbortController local.
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  const onExternalAbort = () => controller.abort();
  if (signal) {
    if (signal.aborted) controller.abort();
    else signal.addEventListener('abort', onExternalAbort, { once: true });
  }

  const mergedHeaders = new Headers();
  mergedHeaders.set('Accept', 'application/json');
  if (body !== undefined) mergedHeaders.set('Content-Type', 'application/json');
  if (token) mergedHeaders.set('Authorization', `Bearer ${token}`);
  if (headers) {
    new Headers(headers).forEach((value, key) => mergedHeaders.set(key, value));
  }

  const normalizedPath = path.startsWith('/') ? path : `/${path}`;

  let response: Response;
  try {
    response = await fetch(`${getApiBaseUrl()}${normalizedPath}`, {
      method,
      headers: mergedHeaders,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal: controller.signal,
      cache: 'no-store',
    });
  } finally {
    clearTimeout(timeout);
    if (signal) signal.removeEventListener('abort', onExternalAbort);
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
    throw new ApiError(response.status, response.statusText, parsed);
  }

  if (response.status === 204) return undefined as T;
  return parsed as T;
}
