import type { ConversationSummary } from '@/contracts';
import { ApiError, apiFetch } from './browser';

/**
 * Service de conversations — côté navigateur (client components uniquement).
 * ---------------------------------------------------------------------------
 * Wire format camelCase (ConversationSummaryDto.cs) :
 * `{ id, title, createdAt, updatedAt }` — il n'y a PAS de `messageCount`.
 *
 * Le fetch same-origin passe par le proxy BFF `src/app/api/[...path]/route.ts`
 * qui injecte le Bearer depuis le cookie httpOnly (lib/api/browser.ts).
 * ---------------------------------------------------------------------------
 */

/** GET /api/conversations → liste triée par le backend (plus récente d'abord). */
export async function getConversations(): Promise<ConversationSummary[]> {
  return apiFetch<ConversationSummary[]>('/api/conversations');
}

/**
 * Création d'une conversation — PLACEHOLDER.
 *
 * Vérifié sur src/Agirh.Api/Controllers/ConversationsController.cs : le backend
 * n'expose PAS de `POST /api/conversations` (seuls GET, GET {id}/messages et
 * DELETE existent). La création passe par le flux SSE du chat (Phase 3) : le
 * premier événement `conversation` fournit l'id de la nouvelle conversation.
 * Ce helper est donc volontairement inutilisé en Phase 2 et documente le
 * contrat pour la Phase 3.
 */
export async function createConversation(): Promise<ConversationSummary> {
  throw new ApiError(501, 'La création d’une conversation passe par le chat (Phase 3).');
}
