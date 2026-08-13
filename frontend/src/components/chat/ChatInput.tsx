'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import type { ChangeEvent, KeyboardEvent } from 'react';

import { useChat } from '@/lib/state/ChatProvider';

/** Hauteur max du textarea (~6 lignes à 14px + padding). */
const MAX_TEXTAREA_HEIGHT = 150;

/**
 * Zone de saisie du chat.
 *  - textarea AUTO-EXTENSIBLE (rows=1 → ~6 lignes) via scrollHeight.
 *  - Entrée = envoyer ; Maj+Entrée = retour à la ligne (onKeyDown +
 *    preventDefault sur Enter sans shift).
 *  - Envoyer désactivé si `isStreaming` ou texte vide.
 *  - Bouton « Arrêter » (carré rouge) pendant `isStreaming` → `cancelStream()`
 *    (les tokens partiels sont conservés côté store).
 *  - Focus automatique à l'ouverture et après finalisation de la réponse
 *    (principe 39) — la saisie reste ré-utilisable immédiatement.
 */
export default function ChatInput() {
  const { isStreaming, sendMessage, cancelStream } = useChat();
  const [text, setText] = useState('');
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const resetHeight = () => {
    const element = textareaRef.current;
    if (!element) return;
    element.style.height = 'auto';
  };

  const handleChange = (event: ChangeEvent<HTMLTextAreaElement>) => {
    const element = event.target;
    setText(element.value);
    element.style.height = 'auto';
    element.style.height = `${Math.min(element.scrollHeight, MAX_TEXTAREA_HEIGHT)}px`;
  };

  const handleSend = useCallback(() => {
    const trimmed = text.trim();
    if (!trimmed || isStreaming) return;
    sendMessage(trimmed);
    setText('');
    resetHeight();
  }, [text, isStreaming, sendMessage]);

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      handleSend();
    }
  };

  // Focus à l'ouverture (principe 39).
  useEffect(() => {
    textareaRef.current?.focus();
  }, []);

  // Focus après finalisation de la réponse (stream terminé/annulé).
  useEffect(() => {
    if (!isStreaming) textareaRef.current?.focus();
  }, [isStreaming]);

  const canSend = text.trim().length > 0 && !isStreaming;

  return (
    <div className="shrink-0 border-t border-slate-200 bg-white px-4 py-3 md:px-6">
      <div className="mx-auto flex max-w-3xl items-end gap-2">
        <textarea
          ref={textareaRef}
          value={text}
          onChange={handleChange}
          onKeyDown={handleKeyDown}
          rows={1}
          placeholder="Posez votre question… (Entrée pour envoyer, Maj+Entrée pour un retour ligne)"
          aria-label="Message à envoyer à l’assistant"
          className="max-h-[150px] min-h-[44px] flex-1 resize-none rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm text-slate-900 placeholder:text-slate-400 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
        />

        {isStreaming ? (
          <button
            type="button"
            onClick={cancelStream}
            aria-label="Arrêter la génération"
            title="Arrêter"
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-red-200 bg-red-50 text-red-600 transition hover:bg-red-100"
          >
            <span className="block h-3.5 w-3.5 rounded-[3px] bg-red-600" />
          </button>
        ) : (
          <button
            type="button"
            onClick={handleSend}
            disabled={!canSend}
            aria-label="Envoyer le message"
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-blue-600 text-white transition enabled:hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40"
          >
            <svg viewBox="0 0 16 16" fill="none" aria-hidden="true" className="h-4 w-4">
              <path
                d="M2 8 14 2 8 14 6.8 9.2 2 8Z"
                stroke="currentColor"
                strokeWidth="1.6"
                strokeLinejoin="round"
              />
              <path d="M6.8 9.2 14 2" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
            </svg>
          </button>
        )}
      </div>
    </div>
  );
}
