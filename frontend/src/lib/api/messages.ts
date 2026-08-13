import type { ConversationMessage } from '@/contracts';
import { apiFetch } from './browser';

/**
 * Service des messages de conversation — côté navigateur (client components).
 * ---------------------------------------------------------------------------
 * Wire format camelCase vérifié sur
 * `src/Agirh.Api/Controllers/ConversationsController.cs` + `ConversationMessageDto.cs` :
 * `{ id, role, content, toolCalled, timestamp }` (le champ de date est
 * `timestamp`, PAS `createdAt`).
 *
 * Le fetch same-origin passe par le proxy BFF `src/app/api/[...path]/route.ts`
 * qui injecte le Bearer depuis le cookie httpOnly (lib/api/browser.ts).
 * ---------------------------------------------------------------------------
 */

/** GET /api/conversations/{id}/messages → historique complet (ordre chronologique). */
export async function getConversationMessages(id: string): Promise<ConversationMessage[]> {
  return apiFetch<ConversationMessage[]>(`/api/conversations/${id}/messages`);
}
