'use client';

import { useCallback, useMemo } from 'react';

import SuggestionChips from '@/components/chat/SuggestionChips';
import SalaryAdvanceCard from '@/components/chat/widgets/SalaryAdvanceCard';
import SafeMarkdown from '@/components/ui/SafeMarkdown';
import { parseMarkers } from '@/lib/chat/markers';
import type { MarkerBlock } from '@/lib/chat/markers';
import { useChat } from '@/lib/state/ChatProvider';

interface AssistantContentProps {
  /** Contenu brut FINALISÉ d'une réponse assistant (marqueurs inclus). */
  content: string;
}

/**
 * Rendu d'une réponse assistant FINALISÉE.
 * ---------------------------------------------------------------------------
 * Pipeline : `parseMarkers(content)` → blocs ordonnés :
 *  - blocs `text`    → `<SafeMarkdown />` (Markdown STRICT, @secops-guardian) ;
 *  - blocs `widget`  → `<SalaryAdvanceCard id />` (composant @hexagonal-architect) ;
 *  - blocs `suggest` → collectés, DÉDUPLIQUÉS, groupés en bas de la bulle
 *                      via `<SuggestionChips />` (clic → `sendMessage`).
 *
 * Garanties :
 *  - les marqueurs ne laissent AUCUNE trace dans le rendu (un token invalide
 *    est retiré par `parseMarkers`, jamais affiché brut) ;
 *  - les blocs `text` vides/whitespace sont ignorés au rendu ;
 *  - zéro `dangerouslySetInnerHTML` (SafeMarkdown + texte React).
 * ---------------------------------------------------------------------------
 */
export default function AssistantContent({ content }: AssistantContentProps) {
  const { sendMessage } = useChat();

  const blocks = useMemo<MarkerBlock[]>(() => parseMarkers(content), [content]);

  const suggestions = useMemo<string[]>(() => {
    const seen = new Set<string>();
    const list: string[] = [];
    for (const block of blocks) {
      if (block.kind !== 'suggest') continue;
      const text = block.text.trim();
      if (!text || seen.has(text)) continue;
      seen.add(text);
      list.push(text);
    }
    return list;
  }, [blocks]);

  const handleSuggestSelect = useCallback(
    (text: string) => {
      sendMessage(text);
    },
    [sendMessage],
  );

  return (
    <div className="min-w-0">
      {blocks.map((block, index) => {
        switch (block.kind) {
          case 'text': {
            if (block.content.trim() === '') return null;
            return <SafeMarkdown key={index} content={block.content} />;
          }
          case 'widget':
            return <SalaryAdvanceCard key={index} id={block.id} />;
          case 'suggest':
            // Regroupés en bas de la bulle (dédupliqués).
            return null;
        }
      })}

      {suggestions.length > 0 ? (
        <SuggestionChips suggestions={suggestions} onSelect={handleSuggestSelect} />
      ) : null}
    </div>
  );
}
