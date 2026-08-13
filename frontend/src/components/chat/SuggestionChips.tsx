'use client';

import { useCallback } from 'react';

import { useChat } from '@/lib/state/ChatProvider';

interface SuggestionChipsProps {
  /** Textes des quick replies (déjà dédupliqués par l'appelant). */
  suggestions: string[];
  /**
   * Callback de clic. Si absent, le texte est envoyé via le store
   * (`useChat().sendMessage(text)`) — le provider est donc requis.
   */
  onSelect?: (text: string) => void;
}

/**
 * Quick replies de l'assistant — chips cliquables (style pill).
 * Un clic envoie le texte comme message utilisateur (`sendMessage` du store).
 * Pendant un streaming en cours, les chips sont désactivées (l'envoi serait
 * de toute façon refusé par le store — évite les clics sans effet).
 * Zéro `dangerouslySetInnerHTML` : le label est un texte React encodé.
 */
export default function SuggestionChips({ suggestions, onSelect }: SuggestionChipsProps) {
  const { sendMessage, isStreaming } = useChat();

  const handleSelect = useCallback(
    (text: string) => {
      const trimmed = text.trim();
      if (!trimmed) return;
      if (onSelect) {
        onSelect(trimmed);
        return;
      }
      sendMessage(trimmed);
    },
    [onSelect, sendMessage],
  );

  if (suggestions.length === 0) return null;

  return (
    <div className="mt-3 flex flex-wrap gap-2" role="group" aria-label="Suggestions de réponse">
      {suggestions.map((suggestion, index) => (
        <button
          key={`${suggestion}-${index}`}
          type="button"
          onClick={() => handleSelect(suggestion)}
          disabled={isStreaming}
          className="rounded-full border border-blue-200 bg-blue-50 px-3 py-1.5 text-xs font-medium text-blue-700 transition hover:border-blue-300 hover:bg-blue-100 hover:text-blue-800 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {suggestion}
        </button>
      ))}
    </div>
  );
}
