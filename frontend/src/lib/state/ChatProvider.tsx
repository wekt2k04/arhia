'use client';

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  useRef,
} from 'react';
import type { ReactNode } from 'react';

import type { ConversationMessage } from '@/contracts';
import { getConversationMessages } from '@/lib/api/messages';
import { streamChat } from '@/lib/api/chat';

/**
 * ─────────────────────────────────────────────────────────────────────────────
 *  STORE DU CHAT — Phase 3 (React Context + useReducer, zéro dépendance).
 * ─────────────────────────────────────────────────────────────────────────────
 *  Détient TOUT l'état de la conversation active :
 *    messages, conversationId, isStreaming, isLoadingHistory, error.
 *
 *  Les effets de bord (fetch historique, stream SSE) vivent DANS le provider
 *  (pas dans les composants) : la UI consomme le store par `useChat()`.
 *
 *  Le streaming consomme le contrat `streamChat` (src/lib/api/chat.ts, écrit
 *  par l'agent @ai-rag-specialist) — signature contractuelle :
 *    streamChat({ message, conversationId?, previousMessages, signal?,
 *                 onConversation?, onTokens?(chunk: string[]),
 *                 onDenied?(message), onError?(friendlyMessage) })
 *                  → Promise<StreamResult>
 * ---------------------------------------------------------------------------
 */

export type ChatRole = 'user' | 'assistant';
export type ChatMessageType = 'normal' | 'denied' | 'error';

/** Message du store (état UI local — PAS le wire backend). */
export interface ChatMessage {
  id: string;
  role: ChatRole;
  content: string;
  /** `normal` par défaut ; `denied`/`error` sont des décorations UI. */
  type?: ChatMessageType;
  /** ISO 8601 — mappé depuis `timestamp` (wire) ou `new Date()` (local). */
  createdAt: string;
}

interface ChatState {
  messages: ChatMessage[];
  conversationId: string | null;
  isStreaming: boolean;
  isLoadingHistory: boolean;
  /** Erreur de chargement d'historique (tolérante, non bloquante). */
  error: string | null;
  /** Id du message assistant en cours de streaming (réduit les courses). */
  streamingAssistantId: string | null;
}

const initialState: ChatState = {
  messages: [],
  conversationId: null,
  isStreaming: false,
  isLoadingHistory: false,
  error: null,
  streamingAssistantId: null,
};

/** Nombre max de messages envoyés au LLM en contexte (contrat streamChat). */
const PREVIOUS_MESSAGES_LIMIT = 20;

/** Message d'erreur utilisateur GÉNÉRIQUE (jamais technique, principe 49). */
const HISTORY_ERROR_MESSAGE =
  'Impossible de charger l’historique. Vous pouvez continuer à écrire un message.';

type ChatAction =
  | { type: 'send_start'; userMessage: ChatMessage; assistantId: string }
  | { type: 'append_tokens'; assistantId: string; tokens: string[] }
  | { type: 'stream_end'; assistantId: string }
  | { type: 'deny_message'; assistantId: string; message: string }
  | { type: 'stream_error'; assistantId: string; friendlyMessage: string }
  | { type: 'set_conversation'; conversationId: string }
  | { type: 'load_start'; conversationId: string }
  | { type: 'load_success'; conversationId: string; messages: ChatMessage[] }
  | { type: 'load_failure' }
  | { type: 'new_conversation' };

/** Id local unique — crypto.randomUUID (localhost = contexte sécurisé), fallback. */
function createId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function emptyAssistantMessage(id: string): ChatMessage {
  return { id, role: 'assistant', content: '', createdAt: new Date().toISOString() };
}

/** Wire backend → message du store (timestamp camelCase vérifié contrôleur). */
function wireMessageToChatMessage(message: ConversationMessage): ChatMessage {
  return {
    id: message.id,
    role: message.role === 'user' ? 'user' : 'assistant',
    content: message.content,
    type: 'normal',
    createdAt: message.timestamp,
  };
}

function reducer(state: ChatState, action: ChatAction): ChatState {
  switch (action.type) {
    case 'send_start': {
      const assistant = emptyAssistantMessage(action.assistantId);
      return {
        ...state,
        messages: [...state.messages, action.userMessage, assistant],
        isStreaming: true,
        isLoadingHistory: false,
        error: null,
        streamingAssistantId: action.assistantId,
      };
    }

    case 'append_tokens': {
      const text = action.tokens.join('');
      if (!text) return state;
      return {
        ...state,
        messages: state.messages.map((message) =>
          message.id === action.assistantId
            ? { ...message, content: message.content + text }
            : message,
        ),
      };
    }

    case 'stream_end': {
      // Les tokens partiels sont CONSERVÉS : on ne touche pas au contenu.
      return {
        ...state,
        isStreaming: false,
        streamingAssistantId:
          state.streamingAssistantId === action.assistantId ? null : state.streamingAssistantId,
      };
    }

    case 'deny_message': {
      return {
        ...state,
        isStreaming: false,
        streamingAssistantId: null,
        messages: state.messages.map((message) =>
          message.id === action.assistantId
            ? { ...message, type: 'denied', content: action.message }
            : message,
        ),
      };
    }

    case 'stream_error': {
      return {
        ...state,
        isStreaming: false,
        streamingAssistantId: null,
        messages: state.messages.map((message) =>
          message.id === action.assistantId
            ? { ...message, type: 'error', content: action.friendlyMessage }
            : message,
        ),
      };
    }

    case 'set_conversation': {
      return { ...state, conversationId: action.conversationId };
    }

    case 'load_start': {
      return {
        ...state,
        conversationId: action.conversationId,
        messages: [],
        isLoadingHistory: true,
        error: null,
        streamingAssistantId: null,
      };
    }

    case 'load_success': {
      return {
        ...state,
        conversationId: action.conversationId,
        messages: action.messages,
        isLoadingHistory: false,
        error: null,
      };
    }

    case 'load_failure': {
      // Tolérant : la saisie reste utilisable (principe 49), aucune crash.
      return { ...state, isLoadingHistory: false, error: HISTORY_ERROR_MESSAGE };
    }

    case 'new_conversation': {
      return { ...initialState };
    }

    default:
      return state;
  }
}

/* ─────────────────────────── Contexte public ─────────────────────────── */

export interface ChatContextValue {
  messages: ChatMessage[];
  conversationId: string | null;
  isStreaming: boolean;
  isLoadingHistory: boolean;
  error: string | null;
  /** Id du message assistant en cours de streaming — permet à MessageList de
   *  passer `streaming` à MessageBubble (rendu brut pendant le flux). */
  streamingAssistantId: string | null;
  selectConversation: (id: string | null) => void;
  loadMessages: (id: string) => void;
  sendMessage: (text: string) => void;
  appendTokens: (chunk: string[]) => void;
  finalizeAssistant: () => void;
  cancelStream: () => void;
  deny: (message: string) => void;
  streamError: (friendlyMessage: string) => void;
  newConversation: () => void;
}

const ChatContext = createContext<ChatContextValue | null>(null);

interface ChatProviderProps {
  children: ReactNode;
  /**
   * Appelé quand le serveur attribue une conversation à un premier message
   * (événement `conversation` du flux) — le parent rafraîchit la sidebar.
   */
  onConversationCreated?: (conversationId: string) => void;
}

/**
 * Fournit le store du chat. Le provider est le SEUL endroit qui :
 *  - appelle `streamChat` (contrat @ai-rag-specialist),
 *  - appelle `getConversationMessages` (fetch historique).
 */
export function ChatProvider({ children, onConversationCreated }: ChatProviderProps) {
  const [state, dispatch] = useReducer(reducer, initialState);

  // Dernier état COMMITTÉ pour les lecteurs dans les callbacks/event handlers
  // (évite les fermetures périmées de useReducer).
  const stateRef = useRef(state);
  useEffect(() => {
    stateRef.current = state;
  }, [state]);

  // Contrôleur d'abandon du flux courant + garde « annulation volontaire ».
  const abortRef = useRef<AbortController | null>(null);
  const cancelRequestedRef = useRef(false);

  // Séquence de chargement : ignore les réponses devenues obsolètes.
  const loadSeqRef = useRef(0);

  const abortActiveStream = useCallback(() => {
    const controller = abortRef.current;
    if (!controller) return;
    cancelRequestedRef.current = true;
    controller.abort();
  }, []);

  const loadMessages = useCallback((id: string) => {
    const seq = ++loadSeqRef.current;
    dispatch({ type: 'load_start', conversationId: id });

    void getConversationMessages(id)
      .then((wireMessages) => {
        if (seq !== loadSeqRef.current) return;

        const loaded = [...wireMessages]
          // Défensif : ordre chronologique garanti même si le backend change.
          .sort((a, b) => a.timestamp.localeCompare(b.timestamp))
          .map(wireMessageToChatMessage);

        dispatch({ type: 'load_success', conversationId: id, messages: loaded });
      })
      .catch(() => {
        if (seq !== loadSeqRef.current) return;
        dispatch({ type: 'load_failure' });
      });
  }, []);

  const selectConversation = useCallback(
    (id: string | null) => {
      // Toute sélection annule un flux en cours (les tokens partiels de la
      // conversation précédente restent dans le store, hors écran).
      abortActiveStream();
      loadSeqRef.current += 1;
      if (id === null) {
        dispatch({ type: 'new_conversation' });
        return;
      }
      loadMessages(id);
    },
    [abortActiveStream, loadMessages],
  );

  const newConversation = useCallback(() => {
    abortActiveStream();
    loadSeqRef.current += 1;
    dispatch({ type: 'new_conversation' });
  }, [abortActiveStream]);

  const sendMessage = useCallback(
    (text: string) => {
      const trimmed = text.trim();
      if (!trimmed) return;

      const current = stateRef.current;
      // Un seul envoi à la fois (spec). On attend aussi la fin du chargement
      // d'historique : l'écraser avec un message frais créerait une course
      // (le skeleton est bref, l'input reste focalisé et réutilisable).
      if (current.isStreaming) return;
      if (current.isLoadingHistory) return;

      const assistantId = createId();
      const userMessage: ChatMessage = {
        id: createId(),
        role: 'user',
        content: trimmed,
        createdAt: new Date().toISOString(),
      };

      // Contexte : derniers messages de la conversation courante (≤20,
      // chronologiques), hors décorations UI (erreurs) et tours vides.
      const previousMessages = current.messages
        .filter(
          (message) =>
            message.type !== 'error' &&
            message.content.trim() !== '' &&
            (message.role === 'user' || message.role === 'assistant'),
        )
        .slice(-PREVIOUS_MESSAGES_LIMIT)
        .map((message) => ({ role: message.role, content: message.content }));

      cancelRequestedRef.current = false;
      dispatch({ type: 'send_start', userMessage, assistantId });

      const controller = new AbortController();
      abortRef.current = controller;

      let ended = false;
      const end = (action: ChatAction | null) => {
        if (ended) return;
        ended = true;
        if (abortRef.current === controller) abortRef.current = null;
        if (action) dispatch(action);
        // Finalisation : isStreaming=false, tokens partiels CONSERVÉS.
        dispatch({ type: 'stream_end', assistantId });
      };

      void streamChat({
        message: trimmed,
        conversationId: current.conversationId ?? undefined,
        previousMessages,
        signal: controller.signal,
        onConversation: (conversationId) => {
          const latest = stateRef.current;
          if (latest.conversationId === conversationId) return;
          dispatch({ type: 'set_conversation', conversationId });
          onConversationCreated?.(conversationId);
        },
        onTokens: (tokens) => {
          dispatch({ type: 'append_tokens', assistantId, tokens });
        },
        onDenied: (message) => {
          end({ type: 'deny_message', assistantId, message });
        },
        onError: (friendlyMessage) => {
          // Une annulation volontaire (Arrêter / changement de conversation)
          // ne doit JAMAIS devenir une bulle d'erreur : on finalise tel quel.
          if (cancelRequestedRef.current) {
            end(null);
            return;
          }
          end({ type: 'stream_error', assistantId, friendlyMessage });
        },
      }).then(
        () => end(null),
        () => end(null),
      );
    },
    [onConversationCreated],
  );

  const cancelStream = useCallback(() => {
    if (!stateRef.current.isStreaming) return;
    abortActiveStream();
  }, [abortActiveStream]);

  // Wrappers publics (actions du store) — agissent sur l'assistant en cours.
  const appendTokens = useCallback((chunk: string[]) => {
    const assistantId = stateRef.current.streamingAssistantId;
    if (assistantId) dispatch({ type: 'append_tokens', assistantId, tokens: chunk });
  }, []);

  const finalizeAssistant = useCallback(() => {
    const assistantId = stateRef.current.streamingAssistantId;
    if (assistantId) dispatch({ type: 'stream_end', assistantId });
  }, []);

  const deny = useCallback((message: string) => {
    const assistantId = stateRef.current.streamingAssistantId;
    if (assistantId) dispatch({ type: 'deny_message', assistantId, message });
  }, []);

  const streamError = useCallback((friendlyMessage: string) => {
    const assistantId = stateRef.current.streamingAssistantId;
    if (assistantId) dispatch({ type: 'stream_error', assistantId, friendlyMessage });
  }, []);

  const value = useMemo<ChatContextValue>(
    () => ({
      messages: state.messages,
      conversationId: state.conversationId,
      isStreaming: state.isStreaming,
      isLoadingHistory: state.isLoadingHistory,
      error: state.error,
      streamingAssistantId: state.streamingAssistantId,
      selectConversation,
      loadMessages,
      sendMessage,
      appendTokens,
      finalizeAssistant,
      cancelStream,
      deny,
      streamError,
      newConversation,
    }),
    [
      state,
      selectConversation,
      loadMessages,
      sendMessage,
      appendTokens,
      finalizeAssistant,
      cancelStream,
      deny,
      streamError,
      newConversation,
    ],
  );

  return <ChatContext.Provider value={value}>{children}</ChatContext.Provider>;
}

/**
 * Accès au store du chat. À utiliser dans n'importe quel composant sous
 * `<ChatProvider>` (ChatShell branche le provider à la racine du chat).
 */
export function useChat(): ChatContextValue {
  const context = useContext(ChatContext);
  if (!context) {
    throw new Error('useChat doit être utilisé dans un <ChatProvider>.');
  }
  return context;
}
