'use client';

import { stripPartialMarkers } from '@/lib/chat/markers';
import type { ChatMessage } from '@/lib/state/ChatProvider';
import AssistantContent from './AssistantContent';

interface MessageBubbleProps {
  message: ChatMessage;
  /**
   * True si ce message est le tour assistant actuellement streamé
   * (fourni par MessageList via `message.id === streamingAssistantId`).
   * Pendant le flux : rendu BRUT anti-flash (`stripPartialMarkers`) pour
   * éviter les sauts visuels du Markdown partiel ET le flash des marqueurs ;
   * à la finalisation : `<AssistantContent />` (markers + SafeMarkdown).
   */
  streaming?: boolean;
}

/**
 * Bulle d'un message du chat.
 *  - user : aligné droite, fond bleu, texte BRUT (encodé par défaut — aucun
 *    rendu Markdown du contenu utilisateur).
 *  - assistant : aligné gauche, fond blanc. Streaming → texte brut filtré par
 *    `stripPartialMarkers` (anti-flash des marqueurs `||WIDGET…||` /
 *    `||SUGGEST…||` partiels) ; finalisé → `<AssistantContent />` qui parse
 *    les marqueurs (Markdown strict, carte SalaryAdvance, chips SUGGEST).
 *  - type='error' : texte gris italique, non intrusif, JAMAIS de Markdown.
 *  - type='denied' : ALERTE ORANGE (fond `bg-orange-50` + bordure
 *    `border-orange-400`, PAS seulement la bordure) + icône d'alerte
 *    (`role="img"` / `aria-label="Accès refusé"`) + `role="alert"` pour
 *    l'annonce aux lecteurs d'écran. Rendu via AssistantContent (les
 *    marqueurs éventuels restent gérés ; `stripControlSentinel` dans
 *    parseMarkers retire toute trace `\u001f`/`DENIED` en défense profonde).
 * Aucun `dangerouslySetInnerHTML` — contenu rendu en texte brut ou via
 * react-markdown (JSX).
 */
export default function MessageBubble({ message, streaming = false }: MessageBubbleProps) {
  const isUser = message.role === 'user';
  const isError = message.type === 'error';
  const isDenied = message.type === 'denied';

  // Bulle d'erreur : discrète, sans cadre de chat.
  if (isError) {
    return (
      <div className="flex justify-start px-1" data-testid="error-bubble">
        <p className="max-w-[85%] text-xs italic leading-relaxed text-slate-400">
          {message.content}
        </p>
      </div>
    );
  }

  // Bulle de REFUS RBAC (événement SSE `denied`) : ALERTE ORANGE (fond +
  // bordure) + icône + `role="alert"` (annonce lecteurs d'écran). Le contenu
  // reste rendu via AssistantContent : les marqueurs éventuels sont gérés et
  // `stripControlSentinel` (parseMarkers) retire toute trace `\u001f`/`DENIED`.
  if (isDenied) {
    return (
      <div className="flex justify-start px-1">
        <div
          role="alert"
          data-denied="true"
          className="min-w-0 max-w-[85%] rounded-2xl rounded-bl-md border-2 border-orange-400 bg-orange-50 px-4 py-2 text-sm leading-relaxed text-slate-800 shadow-sm"
        >
          <div className="flex items-start gap-2">
            <DeniedIcon />
            <div className="min-w-0 flex-1">
              <AssistantContent content={message.content} />
            </div>
          </div>
        </div>
      </div>
    );
  }

  const isAssistantStreaming = message.role === 'assistant' && streaming;

  return (
    <div className={isUser ? 'flex justify-end px-1' : 'flex justify-start px-1'}>
      <div
        className={[
          'min-w-0 max-w-[85%] rounded-2xl px-4 py-2 text-sm leading-relaxed shadow-sm',
          isUser
            ? 'rounded-br-md border-transparent bg-blue-600 text-white'
            : 'rounded-bl-md border border-slate-200 bg-white text-slate-800',
          // `whitespace-pre-wrap` UNIQUEMENT pour le texte brut : utilisateur
          // et assistant en streaming. Le Markdown finalisé ne doit PAS
          // hériter de cette règle (collage de typographie propre).
          isUser || isAssistantStreaming ? 'whitespace-pre-wrap break-words' : '',
        ].join(' ')}
      >
        {isUser ? (
          message.content
        ) : isAssistantStreaming ? (
          // Rendu BRUT anti-flash : les marqueurs (complets ou partiels)
          // disparaissent, le texte reste intact (le store n'est pas modifié).
          stripPartialMarkers(message.content)
        ) : (
          <AssistantContent content={message.content} />
        )}
      </div>
    </div>
  );
}

/**
 * Icône d'alerte (triangle) — SVG inline, AUCUNE ressource externe (CSP
 * Phase 4 inchangée : pas de fetch, pas de style inline, `currentColor`).
 * `role="img"` + `aria-label="Accès refusé"` : l'icône est annoncée par les
 * lecteurs d'écran. `text-orange-700` sur `bg-orange-50` : contraste ≈ 4,9:1
 * (WCAG 1.4.11 non-text ≥ 3:1) — `text-orange-500` serait sous le seuil.
 */
function DeniedIcon() {
  return (
    <svg
      role="img"
      aria-label="Accès refusé"
      className="mt-0.5 h-5 w-5 shrink-0 text-orange-700"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z" />
      <line x1="12" x2="12" y1="9" y2="13" />
      <line x1="12" x2="12.01" y1="17" y2="17" />
    </svg>
  );
}
