/**
 * Contrats de conversation AGIRH.
 * ---------------------------------------------------------------------------
 * Wire format : camelCase (défauts MVC .NET 8).
 * Sources backend :
 *   - src/Agirh.Api/Controllers/ConversationsController.cs
 *   - src/Agirh.Api/Dtos/ConversationSummaryDto.cs
 *   - src/Agirh.Api/Dtos/ConversationMessageDto.cs
 *   - src/Agirh.Api/Controllers/AgentController.cs (ChatRequest / PreviousMessage)
 * ---------------------------------------------------------------------------
 */

/** GET /api/conversations → ConversationSummaryDto[]. */
export interface ConversationSummary {
  id: string;
  title: string;
  createdAt: string; // ISO 8601
  updatedAt: string; // ISO 8601
}

/** GET /api/conversations/{id}/messages → ConversationMessageDto[]. */
export interface ConversationMessage {
  id: string;
  role: 'user' | 'assistant' | (string & {});
  content: string;
  toolCalled: string | null;
  timestamp: string; // ISO 8601
}

/** POST /api/agent/chat — historique envoyé par le client (RawMessage). */
export interface PreviousMessage {
  role: string;
  content: string;
  timestamp: string; // ISO 8601
}

/** POST /api/agent/chat — corps attendu (AgentController.ChatRequest). */
export interface ChatRequest {
  message: string;
  conversationId: string | null;
  previousMessages: PreviousMessage[] | null;
}
