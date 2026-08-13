'use client';

import { useState } from 'react';
import type { ReactNode } from 'react';

import Sidebar from './Sidebar';

interface AppShellProps {
  children: ReactNode;
  /** Conversation active (état porté par ChatShell / le store). */
  activeConversationId: string | null;
  /** Sélection dans la sidebar : `null` = nouvelle discussion. */
  onSelectConversation: (id: string | null) => void;
  /** Incrémentez pour recharger la liste de la sidebar (nouvelle conversation). */
  sidebarRefreshKey?: number;
}

/**
 * Shell applicatif du chat AGIRH.
 *  - hauteur 100dvh (`h-dvh`) : pas de saut d'UI sur mobile (URL bar).
 *  - header fixe h-16 (bouton hamburger `md:hidden` → drawer mobile).
 *  - main `flex-1 min-h-0` : scroll interne uniquement, le body ne scrolle
 *    JAMAIS.
 *  - sidebar desktop (md+) en colonne `w-64` ; drawer off-canvas sur mobile
 *    (`translate-x`, backdrop, fermeture au clic / à la sélection).
 *
 * Aucune logique d'authentification ni de données : les états et le fetch
 * vivent dans les parents (ChatShell) et dans `lib/api/*`.
 */
export default function AppShell({
  children,
  activeConversationId,
  onSelectConversation,
  sidebarRefreshKey,
}: AppShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);

  const closeDrawer = () => setDrawerOpen(false);

  return (
    <div className="flex h-dvh flex-col overflow-hidden bg-slate-50">
      <header className="flex h-16 shrink-0 items-center justify-between border-b border-slate-200 bg-white px-4 md:px-6">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => setDrawerOpen((open) => !open)}
            aria-label={
              drawerOpen ? 'Fermer le menu des conversations' : 'Ouvrir le menu des conversations'
            }
            aria-expanded={drawerOpen}
            className="rounded-md p-2 text-slate-600 transition hover:bg-slate-100 md:hidden"
          >
            {drawerOpen ? (
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true" className="h-5 w-5">
                <path
                  d="M6 6l12 12M18 6L6 18"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true" className="h-5 w-5">
                <path
                  d="M4 7h16M4 12h16M4 17h16"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                />
              </svg>
            )}
          </button>

          <div className="flex items-baseline gap-2">
            <span className="text-sm font-semibold tracking-tight text-slate-900">AGIRH</span>
            <span className="text-xs text-slate-400">Assistant RH</span>
          </div>
        </div>

        <div className="text-xs text-slate-400">Historique</div>
      </header>

      <div className="flex min-h-0 flex-1">
        <Sidebar
          activeId={activeConversationId}
          onSelect={onSelectConversation}
          drawerOpen={drawerOpen}
          onDrawerClose={closeDrawer}
          refreshKey={sidebarRefreshKey}
        />

        <main className="flex min-h-0 min-w-0 flex-1 flex-col">
          <div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
        </main>
      </div>
    </div>
  );
}
