'use client';

import { useEffect, useRef, useState } from 'react';

import { useChat } from '@/lib/state/ChatProvider';
import MessageBubble from './MessageBubble';
import TypingIndicator from './TypingIndicator';

/** Seuil (px) : tant que le bas est à moins de 80px, l'auto-scroll reste actif. */
const NEAR_BOTTOM_THRESHOLD = 80;

/**
 * Liste des messages du chat (zone centrale).
 *  - Zone scrollable `flex-1 min-h-0` (le body ne scrolle JAMAIS).
 *  - Auto-scroll INTELLIGENT : ne scrolle que si l'utilisateur est proche du
 *    bas (`scrollHeight - scrollTop - clientHeight < 80px`, via onScroll).
 *    Si l'utilisateur a remonté pendant la génération → l'auto-scroll est
 *    désactivé et un bouton flottant « ↓ Revenir en bas » apparaît.
 *  - Skeleton pendant `isLoadingHistory` ; note tolérante si erreur
 *    d'historique (principe 49) ; TypingIndicator avant le premier token.
 * Pas de liste virtuelle : histoires courtes (limite backend par rôle).
 */
export default function MessageList() {
  const { messages, isStreaming, isLoadingHistory, error, streamingAssistantId } = useChat();

  const containerRef = useRef<HTMLDivElement>(null);
  const [isNearBottom, setIsNearBottom] = useState(true);

  const scrollToBottom = () => {
    const element = containerRef.current;
    if (!element) return;
    element.scrollTo({ top: element.scrollHeight, behavior: 'smooth' });
  };

  const handleScroll = () => {
    const element = containerRef.current;
    if (!element) return;
    const distance = element.scrollHeight - element.scrollTop - element.clientHeight;
    setIsNearBottom(distance < NEAR_BOTTOM_THRESHOLD);
  };

  // Auto-scroll : uniquement si l'utilisateur est (encore) proche du bas.
  useEffect(() => {
    if (!isNearBottom) return;
    const element = containerRef.current;
    if (!element) return;
    element.scrollTop = element.scrollHeight;
  }, [messages, isNearBottom]);

  // Pendant la génération, un assistant vide = en attente du premier token.
  const showTyping =
    isStreaming &&
    messages.length > 0 &&
    messages[messages.length - 1].role === 'assistant' &&
    messages[messages.length - 1].content === '';

  return (
    <div className="relative min-h-0 flex-1">
      <div
        ref={containerRef}
        onScroll={handleScroll}
        role="log"
        aria-live="polite"
        aria-busy={isStreaming || isLoadingHistory}
        className="h-full overflow-y-auto px-4 py-4"
      >
        {isLoadingHistory ? (
          <HistorySkeleton />
        ) : error ? (
          <div className="flex h-full items-start justify-center px-4 pt-8">
            <p className="max-w-md text-center text-xs italic text-slate-400">{error}</p>
          </div>
        ) : messages.length === 0 ? (
          <EmptyState />
        ) : (
          <div className="mx-auto flex max-w-3xl flex-col gap-3">
            {messages.map((message) => (
              <MessageBubble
                key={message.id}
                message={message}
                streaming={message.id === streamingAssistantId}
              />
            ))}

            {showTyping ? (
              <div className="flex justify-start px-1">
                <div className="rounded-2xl rounded-bl-md border border-slate-200 bg-white px-4 py-3 shadow-sm">
                  <TypingIndicator />
                </div>
              </div>
            ) : null}
          </div>
        )}
      </div>

      {/* Bouton flottant : visible dès que l'utilisateur a remonté. */}
      {!isNearBottom ? (
        <button
          type="button"
          onClick={scrollToBottom}
          className="absolute bottom-4 right-4 z-10 flex items-center gap-1.5 rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 shadow-sm transition hover:bg-slate-50"
        >
          <svg viewBox="0 0 16 16" fill="none" aria-hidden="true" className="h-3.5 w-3.5">
            <path
              d="M8 3v10M3.5 8.5 8 13l4.5-4.5"
              stroke="currentColor"
              strokeWidth="1.8"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          Revenir en bas
        </button>
      ) : null}
    </div>
  );
}

/** Skeleton de chargement de l'historique — 3 barres shimmer. */
function HistorySkeleton() {
  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-3" role="status" aria-busy="true">
      <span className="sr-only">Chargement des messages…</span>
      {[0, 1, 2].map((index) => (
        <div
          key={index}
          className={[
            'h-14 animate-pulse rounded-2xl bg-slate-200/80',
            index % 2 === 0 ? 'w-2/3 self-end rounded-br-md' : 'w-3/4 self-start rounded-bl-md',
          ].join(' ')}
        />
      ))}
    </div>
  );
}

/** État vide — nouvelle discussion. */
function EmptyState() {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-2 px-4 text-center">
      <p className="text-sm font-medium text-slate-500">Assistant RH AGIRH</p>
      <p className="max-w-md text-xs leading-relaxed text-slate-400">
        Écrivez votre première question pour démarrer la discussion. Entrée pour
        envoyer, Maj+Entrée pour un retour à la ligne.
      </p>
    </div>
  );
}
