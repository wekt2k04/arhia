/**
 * Contrats SSE du chat agent AGIRH.
 * ---------------------------------------------------------------------------
 * Source backend : src/Agirh.Api/Controllers/AgentController.cs (WriteSseEvent)
 * Format du flux : `data: {"type":"...","data":"..."}\n\n` (2 retours à la ligne).
 *
 * Types d'événement possibles (enumération FERMÉE — validation stricte) :
 *   - "conversation" : id de la conversation (Guid) → TOUJOURS le 1er événement
 *   - "token"        : fragment de texte généré (mot + espace, ~1/30 ms)
 *   - "denied"       : accès refusé — NON émis par AgentController.cs actuel
 *                      (présent dans le legacy Blazor AgentChatService.cs,
 *                      conservé par rétro-compatibilité / contrat Phase 5) ;
 *                      le client le traite en défensif.
 *   - "error"        : erreur (message requis, auth invalide, conversation
 *                      non possédée…)
 *   - "done"         : marqueur terminal, data = "[DONE]"
 *
 * Codes HTTP possibles sur POST /api/agent/chat (vérifiés sur
 * AgentController.cs / Program.cs) :
 *   - 200 : flux SSE normal (Content-Type: text/event-stream). ATTENTION : une
 *           erreur de validation ("Message requis") est renvoyée en 200 AVEC un
 *           événement SSE "error" (les headers SSE ne sont pas encore posés).
 *   - 401 : JSON (BFF sans cookie / JWT rejeté) → redirection login côté client.
 *   - 403 : événement SSE "error" "Cette conversation ne vous appartient pas."
 *           (Content-Type NON SSE : les headers sont posés APRÈS
 *           GetOrCreateConversationAsync — AgentController.cs l. 97).
 *   - 429 : JSON `{ message }` (rate limiter Program.cs — 20 req/min sur chat).
 *   - 5xx : JSON `{ message }` (exception handler Program.cs).
 * ---------------------------------------------------------------------------
 */

export type SseEventType = 'conversation' | 'token' | 'denied' | 'error' | 'done';

export interface SseEventBase {
  type: SseEventType;
  data: string;
}

export interface SseConversationEvent extends SseEventBase {
  type: 'conversation';
  /** Id de la conversation (Guid). */
  data: string;
}

export interface SseTokenEvent extends SseEventBase {
  type: 'token';
  /** Fragment de texte généré par le LLM (mot + espace, sauf dernier mot). */
  data: string;
}

export interface SseDeniedEvent extends SseEventBase {
  type: 'denied';
  data: string;
}

export interface SseErrorEvent extends SseEventBase {
  type: 'error';
  data: string;
}

export interface SseDoneEvent extends SseEventBase {
  type: 'done';
  /** Marqueur terminal : "[DONE]". */
  data: string;
}

export type SseEvent =
  | SseConversationEvent
  | SseTokenEvent
  | SseDeniedEvent
  | SseErrorEvent
  | SseDoneEvent;

export function isSseEventType(value: unknown): value is SseEventType {
  return (
    value === 'conversation' ||
    value === 'token' ||
    value === 'denied' ||
    value === 'error' ||
    value === 'done'
  );
}

/**
 * Parse une frame SSE COMPLÈTE (terminée par `\n\n`), éventuellement multi-lignes.
 *
 * - Collecte les lignes `data:` (les lignes `id:`/`event:`/`retry:`/commentaires
 *   sont ignorées — le backend n'en émet pas).
 * - Les payloads de plusieurs lignes `data:` sont concaténés par `\n`
 *   (règle SSE), puis parsés comme JSON `{ type, data }`.
 * - Retourne `null` si la frame est vide, malformée, sans ligne `data:`,
 *   non-JSON, ou si `type`/`data` ne respectent pas le contrat fermé.
 */
export function parseSseFrame(frame: string): SseEvent | null {
  const dataPayloads: string[] = [];

  for (const rawLine of frame.split('\n')) {
    const line = rawLine.trim();
    if (!line.startsWith('data:')) continue;
    // SSE : un espace optionnel après le deux-points.
    dataPayloads.push(line.slice('data:'.length).replace(/^[ \t]/, ''));
  }

  if (dataPayloads.length === 0) return null;

  try {
    const parsed: unknown = JSON.parse(dataPayloads.join('\n'));
    if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) return null;

    const record = parsed as Record<string, unknown>;
    if (!isSseEventType(record['type']) || typeof record['data'] !== 'string') return null;

    return { type: record['type'], data: record['data'] } as SseEvent;
  } catch {
    return null;
  }
}

/**
 * Parse une ligne brute `data: {...}` du flux SSE (cas mono-ligne).
 * Délègue à `parseSseFrame` (une ligne unique est une frame valide).
 * Retourne `null` si la ligne est vide, malformée ou non-JSON.
 */
export function parseSseData(rawLine: string): SseEvent | null {
  return parseSseFrame(rawLine.trim());
}
