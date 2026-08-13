'use client';

import { useCallback, useEffect, useState } from 'react';

import type { ConversationSummary } from '@/contracts';
import { getConversations } from '@/lib/api/conversations';
import { formatRelativeDate } from '@/lib/format/relativeDate';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'ready'; conversations: ConversationSummary[] };

interface SidebarProps {
  /** Conversation active (id) — surlignée dans la liste. */
  activeId: string | null;
  /** Sélection utilisateur : `null` = nouvelle discussion. */
  onSelect: (id: string | null) => void;
  /** Drawer mobile ouvert (contrôlé par AppShell). */
  drawerOpen: boolean;
  /** Fermeture du drawer mobile (backdrop / sélection). */
  onDrawerClose: () => void;
  /** Incrémentez pour recharger la liste sans perdre l'existant (nouvelle conversation). */
  refreshKey?: number;
}

/**
 * Sidebar de l'historique des conversations AGIRH.
 *
 *  - Desktop (md+) : colonne statique `w-64` dans le layout.
 *  - Mobile (< md) : drawer off-canvas `w-72` piloté par `drawerOpen`
 *    (transitions douces, backdrop de fermeture, fermeture à la sélection).
 *  - UN SEUL rendu (une seule requête) : les classes responsive mutent le
 *    même élément plutôt que de dupliquer le DOM.
 *
 *  États : skeleton de chargement (3 barres shimmer), état vide, état
 *  d'erreur tolérant avec « Réessayer » (principe 49 — l'échec de
 *  l'historique ne doit jamais casser le chat).
 *
 *  Aucune logique métier : le fetch vit dans `lib/api/conversations.ts`.
 */
export default function Sidebar({
  activeId,
  onSelect,
  drawerOpen,
  onDrawerClose,
  refreshKey,
}: SidebarProps) {
  const [state, setState] = useState<LoadState>({ kind: 'loading' });

  const load = useCallback(async () => {
    // Rechargement SILENCIEUX quand une liste existe déjà (pas de flash pendant
    // le streaming après la création d'une conversation).
    setState((prev) => (prev.kind === 'ready' ? prev : { kind: 'loading' }));
    try {
      const conversations = await getConversations();
      setState({ kind: 'ready', conversations });
    } catch {
      // Tolérant : on garde l'existant si on l'avait, sinon erreur (principe 49).
      setState((prev) => (prev.kind === 'ready' ? prev : { kind: 'error' }));
    }
  }, []);

  useEffect(() => {
    load();
  }, [load, refreshKey]);

  function handleSelect(id: string | null) {
    onSelect(id);
    onDrawerClose();
  }

  return (
    <>
      {/* Backdrop mobile : ferme le drawer au clic (masqué au-dessus de md). */}
      {drawerOpen ? (
        <div
          aria-hidden="true"
          onClick={onDrawerClose}
          className="fixed inset-0 z-30 bg-black/40 md:hidden"
        />
      ) : null}

      <aside
        className={[
          'fixed inset-y-0 left-0 z-40 flex w-72 flex-col border-r border-slate-200 bg-white',
          'transition-transform duration-300 ease-in-out',
          drawerOpen ? 'translate-x-0' : '-translate-x-full',
          'md:static md:z-auto md:w-64 md:translate-x-0',
        ].join(' ')}
      >
        <div className="shrink-0 border-b border-slate-100 p-3">
          <button
            type="button"
            onClick={() => handleSelect(null)}
            className="flex w-full items-center justify-center gap-2 rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white transition hover:bg-slate-700"
          >
            <svg viewBox="0 0 16 16" fill="none" aria-hidden="true" className="h-4 w-4">
              <path d="M8 3v10M3 8h10" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
            </svg>
            Nouvelle discussion
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-3">
          {state.kind === 'loading' ? (
            <LoadingSkeleton />
          ) : state.kind === 'error' ? (
            <ErrorState onRetry={load} />
          ) : state.conversations.length === 0 ? (
            <EmptyState />
          ) : (
            <ul className="space-y-1">
              {state.conversations.map((conversation) => {
                const isActive = conversation.id === activeId;
                const title = conversation.title.trim() || 'Sans titre';
                return (
                  <li key={conversation.id}>
                    <button
                      type="button"
                      onClick={() => handleSelect(conversation.id)}
                      aria-current={isActive ? 'true' : undefined}
                      className={[
                        'w-full rounded-md px-3 py-2 text-left transition',
                        isActive
                          ? 'bg-blue-50 text-blue-900 ring-1 ring-inset ring-blue-200'
                          : 'text-slate-700 hover:bg-slate-100',
                      ].join(' ')}
                    >
                      <span className="block truncate text-sm font-medium">{title}</span>
                      <span
                        className={[
                          'mt-0.5 block text-xs',
                          isActive ? 'text-blue-500' : 'text-slate-400',
                        ].join(' ')}
                      >
                        {formatRelativeDate(conversation.updatedAt)}
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </aside>
    </>
  );
}

/** Skeleton de chargement — 3 barres shimmer. */
function LoadingSkeleton() {
  return (
    <div className="space-y-3" role="status" aria-busy="true">
      <span className="sr-only">Chargement des conversations…</span>
      {[0, 1, 2].map((index) => (
        <div key={index} className="h-12 animate-pulse rounded-md bg-slate-200/80" />
      ))}
    </div>
  );
}

/** État vide — aucun historique. */
function EmptyState() {
  return (
    <div className="px-2 py-6 text-center">
      <p className="text-sm font-medium text-slate-500">Aucune conversation</p>
      <p className="mt-1 text-xs text-slate-400">
        Vos échanges avec l’assistant apparaîtront ici.
      </p>
    </div>
  );
}

/** État d'erreur tolérant — le chat reste utilisable (principe 49). */
function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="px-2 py-6 text-center">
      <p className="text-sm font-medium text-slate-600">Impossible de charger l’historique.</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-3 rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 transition hover:bg-slate-50"
      >
        Réessayer
      </button>
    </div>
  );
}
