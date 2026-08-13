/**
 * Client de streaming du chat agent — côté navigateur uniquement.
 * ---------------------------------------------------------------------------
 * Flux : POST /api/agent/chat → proxy BFF (src/app/api/agent/[...path]/route.ts,
 * NE PAS modifier) → AgentController.ChatAsync (backend .NET, text/event-stream).
 *
 * Wire format (vérifié chiffre à chiffre sur AgentController.cs) :
 *   `data: {"type":"<type>","data":"<payload>"}\n\n`
 * Événements (ordre) : `conversation` (1er) → `token`* → `done` | `error` | `denied`.
 * Cadence des tokens : ~1/30 ms (Task.Delay(30) — AgentOrchestratorService.cs l. 210).
 *
 * Contrat SSE complet + spéc du flush : `AGIRH_SIMULATION_RUNBOOK.md` (racine, protocole SSE).
 *
 * Ce module NE parse PAS les marqueurs `||SUGGEST…||` / `||WIDGET…||` :
 * ils transitent tels quels dans les tokens (traités en Phase 5).
 * ---------------------------------------------------------------------------
 */

import { parseSseFrame } from '@/contracts';
import type { SseEvent } from '@/contracts';

/** Signature OBLIGATOIRE — consommée par le store chat (agent hexagonal). */
export interface StreamChatParams {
  message: string;
  conversationId?: string | null;
  /** DERNIERS N messages (<=20), ordre chronologique. */
  previousMessages: { role: 'user' | 'assistant'; content: string }[];
  /** Bouton « Arrêter » → controller.abort() → arrêt propre, tokens conservés. */
  signal?: AbortSignal;
  /** 1er événement SSE : id de la conversation (Guid) — l'UI persiste/sidebar. */
  onConversation?: (id: string) => void;
  /** Lot de tokens (flush ~100 ms ou 5 tokens) — un setState par lot, PAS par token. */
  onTokens?: (chunk: string[]) => void;
  onDenied?: (message: string) => void;
  /** Message utilisateur générique FR — jamais de détail technique. */
  onError?: (friendlyMessage: string) => void;
}

export interface StreamResult {
  /** true UNIQUEMENT si l'événement terminal `done` a été reçu. */
  ok: boolean;
  /** Id reçu via l'événement `conversation` (si le flux l'a fourni). */
  conversationId?: string | null;
  /** true si l'appelant a annulé (signal) — les tokens reçus ne sont PAS perdus. */
  cancelled?: boolean;
}

const CHAT_ENDPOINT = '/api/agent/chat';

/** Flush temporel : lot au plus toutes les ~100 ms (→ ≤10 re-rendus/s). */
const FLUSH_INTERVAL_MS = 100;
/** Flush volumétrique : lot dès 5 tokens accumulés (~150 ms à 1 token/30 ms). */
const FLUSH_BATCH_SIZE = 5;

/** Message utilisateur générique par statut HTTP (jamais de détail technique). */
function genericMessageForStatus(status: number): string {
  if (status === 403) return 'Accès refusé.';
  if (status === 404) return 'Élément introuvable.';
  if (status === 429) return 'Trop de requêtes, réessayez dans quelques instants.';
  if (status >= 500) return 'Service temporairement indisponible. Réessayez plus tard.';
  return 'Une erreur est survenue. Réessayez plus tard.';
}

/**
 * Sérialise l'historique vers le wire `PreviousMessage` (camelCase).
 * Le DTO backend attend `role`, `content` ET `timestamp` (DateTime) — le
 * contrat de signature ne transporte pas les timestamps originaux, on émet
 * l'heure courante ; l'ordre chronologique est préservé par l'ordre du tableau
 * (backend : RecentMessages.TakeLast(3) — AgentController.cs l. 93).
 */
function toWirePreviousMessages(
  previousMessages: StreamChatParams['previousMessages'],
): { role: string; content: string; timestamp: string }[] {
  const timestamp = new Date().toISOString();
  return previousMessages.map((m) => ({
    role: m.role,
    content: m.content,
    timestamp,
  }));
}

/**
 * Tente d'extraire le 1er événement SSE d'un corps HTTP non-2xx.
 * Utile pour le 403 (AgentController écrit un événement SSE `error` AVANT de
 * poser les headers SSE) et les corps JSON d'erreur (retourne null).
 */
function firstSseEventFromText(body: string): SseEvent | null {
  if (!body) return null;
  const normalized = body.replace(/\r\n/g, '\n');
  for (const frame of normalized.split('\n\n')) {
    const event = parseSseFrame(frame);
    if (event) return event;
  }
  return null;
}

/**
 * Envoie un message et streame la réponse SSE de l'agent.
 *
 * Règles :
 *  - POST same-origin `/api/agent/chat` (cookie httpOnly auto, BFF injecte le JWT).
 *  - `previousMessages` est TOUJOURS envoyé (fix mémoire conversationnelle,
 *    absent du Blazor) — l. 76-88 AgentController.cs.
 *  - Lecture par ReadableStream + TextDecoder, découpage par frames `\n\n`.
 *  - 401 → redirection `/login?returnUrl=…` ; 429 → onError dédié ;
 *    403 → corps lu (SSE `denied` si présent, sinon `error`) ; sinon onError FR.
 *  - Flush par lots : onTokens reçoit des lots (≤5 tokens, ≤~10/s), jamais
 *    1 token par token (~33/s côté backend).
 *  - Annulation via `params.signal` : arrêt propre, les tokens déjà reçus
 *    sont livrés (dernier lot flushé), `StreamResult.cancelled = true`.
 */
export async function streamChat(params: StreamChatParams): Promise<StreamResult> {
  const {
    message,
    conversationId = null,
    previousMessages = [],
    signal,
    onConversation,
    onTokens,
    onDenied,
    onError,
  } = params;

  // ── Annulation : le signal externe pilote un controller interne ───────────
  const controller = new AbortController();
  const abortFromExternal = () => controller.abort();
  if (signal) {
    if (signal.aborted) {
      controller.abort();
    } else {
      signal.addEventListener('abort', abortFromExternal, { once: true });
    }
  }

  // ── État du flux ──────────────────────────────────────────────────────────
  let receivedConversationId: string | undefined;
  let doneReceived = false;
  let errorReported = false;

  // ── Buffer de tokens + flush par lots (optimisation re-rendus) ────────────
  let pendingTokens: string[] = [];
  let flushTimer: ReturnType<typeof setTimeout> | null = null;
  let lastFlushAt = Date.now();

  const reportErrorOnce = (friendly: string) => {
    if (errorReported) return;
    errorReported = true;
    try {
      onError?.(friendly);
    } catch {
      /* un callback qui lève ne doit pas casser le flux */
    }
  };

  const flushTokens = () => {
    if (flushTimer !== null) {
      clearTimeout(flushTimer);
      flushTimer = null;
    }
    if (pendingTokens.length === 0) return;
    const batch = pendingTokens;
    pendingTokens = [];
    lastFlushAt = Date.now();
    try {
      onTokens?.(batch);
    } catch {
      /* idem */
    }
  };

  const scheduleFlush = () => {
    if (flushTimer !== null) return;
    const elapsed = Date.now() - lastFlushAt;
    const delay = Math.max(0, FLUSH_INTERVAL_MS - elapsed);
    flushTimer = setTimeout(() => {
      flushTimer = null;
      flushTokens();
    }, delay);
  };

  const pushToken = (token: string) => {
    if (!token) return;
    pendingTokens.push(token);
    if (pendingTokens.length >= FLUSH_BATCH_SIZE) {
      flushTokens();
    } else {
      scheduleFlush();
    }
  };

  /** Arrêt terminal : dernier lot livré (aucun token perdu), listener retiré. */
  const finish = () => {
    if (flushTimer !== null) {
      clearTimeout(flushTimer);
      flushTimer = null;
    }
    if (pendingTokens.length > 0) {
      const batch = pendingTokens;
      pendingTokens = [];
      try {
        onTokens?.(batch);
      } catch {
        /* idem */
      }
    }
    if (signal) signal.removeEventListener('abort', abortFromExternal);
  };

  const handleEvent = (event: SseEvent): boolean => {
    switch (event.type) {
      case 'conversation': {
        const id = event.data.trim();
        if (id) {
          receivedConversationId = id;
          try {
            onConversation?.(id);
          } catch {
            /* idem */
          }
        }
        return true;
      }
      case 'token':
        pushToken(event.data);
        return true;
      case 'denied':
        try {
          onDenied?.(event.data);
        } catch {
          /* idem */
        }
        return false; // fin de flux
      case 'error':
        reportErrorOnce(event.data || 'Une erreur est survenue. Réessayez plus tard.');
        return false; // fin de flux
      case 'done':
        doneReceived = true;
        return false; // arrêt de lecture — ok = true
      default:
        return true; // défensif : un type futur inconnu n'interrompt pas
    }
  };

  // ── 1. POST → BFF (same-origin, cookie httpOnly auto) ─────────────────────
  let response: Response;
  try {
    response = await fetch(CHAT_ENDPOINT, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'text/event-stream',
      },
      body: JSON.stringify({
        message,
        conversationId,
        previousMessages: toWirePreviousMessages(previousMessages),
      }),
      signal: controller.signal,
      cache: 'no-store',
    });
  } catch {
    finish();
    if (controller.signal.aborted) {
      return { ok: false, conversationId: receivedConversationId, cancelled: true };
    }
    reportErrorOnce('Connexion impossible. Vérifiez votre réseau puis réessayez.');
    return { ok: false, conversationId: receivedConversationId, cancelled: false };
  }

  // ── 2. Statuts HTTP non-2xx (jamais de détail technique à l'UI) ───────────
  if (response.status === 401) {
    finish();
    if (typeof window !== 'undefined') {
      const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
      window.location.replace(`/login?returnUrl=${returnUrl}`);
    }
    return { ok: false, conversationId: receivedConversationId, cancelled: false };
  }

  if (!response.ok) {
    const body = await response.text().catch(() => '');
    finish();

    if (response.status === 429) {
      // Rate limiter Program.cs : 20 requêtes/min sur /api/agent/chat.
      reportErrorOnce('Trop de requêtes, réessayez dans quelques instants.');
      return { ok: false, conversationId: receivedConversationId, cancelled: false };
    }

    if (response.status === 403) {
      // AgentController l. 62-67 : 403 → événement SSE "error" (« Cette
      // conversation ne vous appartient pas. »). `denied` n'est pas émis par
      // l'API actuelle — on le gère en défensif (compat Phase 5).
      const event = firstSseEventFromText(body);
      if (event?.type === 'denied') {
        try {
          onDenied?.(event.data);
        } catch {
          /* idem */
        }
        return { ok: false, conversationId: receivedConversationId, cancelled: false };
      }
      reportErrorOnce(event?.type === 'error' && event.data ? event.data : 'Accès refusé.');
      return { ok: false, conversationId: receivedConversationId, cancelled: false };
    }

    const event = firstSseEventFromText(body);
    reportErrorOnce(
      event?.type === 'error' && event.data ? event.data : genericMessageForStatus(response.status),
    );
    return { ok: false, conversationId: receivedConversationId, cancelled: false };
  }

  if (!response.body) {
    finish();
    reportErrorOnce('Le serveur n’a pas renvoyé de flux. Réessayez.');
    return { ok: false, conversationId: receivedConversationId, cancelled: false };
  }

  // ── 3. Lecture du flux : ReadableStream → frames `\n\n` → événements ──────
  const reader = response.body.getReader();
  const decoder = new TextDecoder('utf-8');
  let streamBuffer = '';

  const processFrames = (): boolean => {
    let frameEnd = streamBuffer.indexOf('\n\n');
    while (frameEnd !== -1) {
      const frame = streamBuffer.slice(0, frameEnd);
      streamBuffer = streamBuffer.slice(frameEnd + 2);
      const event = parseSseFrame(frame);
      if (event && !handleEvent(event)) return false;
      frameEnd = streamBuffer.indexOf('\n\n');
    }
    return true;
  };

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      streamBuffer += decoder.decode(value, { stream: true }).replace(/\r\n/g, '\n');
      if (!processFrames()) break;
    }
    // Derniers octets multi-octets en attente + éventuelle frame finale.
    streamBuffer += decoder.decode().replace(/\r\n/g, '\n');
    processFrames();
  } catch {
    // Seule exception attendue : abort (bouton « Arrêter » / signal externe).
    // Toute autre erreur réseau est traitée ci-dessous en message générique.
  } finally {
    void reader.cancel().catch(() => {});
  }

  finish();

  if (controller.signal.aborted) {
    return { ok: false, conversationId: receivedConversationId, cancelled: true };
  }

  // ── 4. Résultat : ok UNIQUEMENT si `done` a été reçu ──────────────────────
  if (doneReceived) {
    return { ok: true, conversationId: receivedConversationId, cancelled: false };
  }

  // Flux terminé sans `done` (connexion coupée, exception backend mid-stream…)
  if (!errorReported) {
    reportErrorOnce('La réponse a été interrompue. Réessayez.');
  }
  return { ok: false, conversationId: receivedConversationId, cancelled: false };
}
