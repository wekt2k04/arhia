'use client';

import { useEffect, useState } from 'react';

const PHRASES = [
  'L’assistant analyse votre demande…',
  'Recherche des informations…',
  'Vérification des données…',
  'Rédaction de la réponse…',
  'Un instant…',
];

const ROTATION_INTERVAL_MS = 2500;

/**
 * Indicateur « l'assistant écrit » — barres animées façon égaliseur audio
 * (keyframes .agent-wave-bar dans src/app/globals.css) + texte décoratif qui
 * tourne parmi PHRASES. Affiché pendant l'attente du premier token.
 * Le texte rotatif est purement décoratif (aria-hidden) : seule l'annonce
 * statique de la racine (role="status") est exposée aux lecteurs d'écran,
 * pour éviter une réannonciation à chaque changement de phrase.
 */
export default function TypingIndicator() {
  const [phraseIndex, setPhraseIndex] = useState(0);

  useEffect(() => {
    const id = window.setInterval(() => {
      setPhraseIndex((current) => (current + 1) % PHRASES.length);
    }, ROTATION_INTERVAL_MS);
    return () => window.clearInterval(id);
  }, []);

  return (
    <div
      className="flex items-center gap-2"
      role="status"
      aria-label="L’assistant est en train d’écrire"
      aria-live="polite"
    >
      <div className="flex items-end gap-1" aria-hidden="true">
        <span className="agent-wave-bar h-2 w-1 rounded-full bg-blue-500" />
        <span className="agent-wave-bar h-3 w-1 rounded-full bg-blue-500" style={{ animationDelay: '0.15s' }} />
        <span className="agent-wave-bar h-2 w-1 rounded-full bg-blue-500" style={{ animationDelay: '0.3s' }} />
      </div>
      <span className="text-sm text-slate-500" aria-hidden="true">
        {PHRASES[phraseIndex]}
      </span>
    </div>
  );
}
