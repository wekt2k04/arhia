'use client';

import { useCallback, useState } from 'react';

import AppShell from '@/components/layout/AppShell';
import ChatInput from '@/components/chat/ChatInput';
import MessageList from '@/components/chat/MessageList';
import { ChatProvider, useChat } from '@/lib/state/ChatProvider';

/**
 * Coque du chat (client) — Phase 3.
 *  - Branche `ChatProvider` à la racine : le store (useReducer) détient
 *    messages / conversationId / isStreaming / isLoadingHistory / error.
 *  - Rend `AppShell` + `Sidebar` (via AppShell) reliées au store :
 *      sélection → `selectConversation(id)` / `newConversation()`
 *  - Rafraîchit la sidebar après le PREMIER message d'une nouvelle
 *    conversation (callback `onConversationCreated` du provider).
 *  - Zone centrale : `MessageList` (scroll) + `ChatInput` (saisie).
 */
export default function ChatShell() {
  // Clé de rafraîchissement de la sidebar : +1 à chaque conversation créée.
  const [sidebarRefreshKey, setSidebarRefreshKey] = useState(0);

  const handleConversationCreated = useCallback(() => {
    setSidebarRefreshKey((key) => key + 1);
  }, []);

  return (
    <ChatProvider onConversationCreated={handleConversationCreated}>
      <ChatContent sidebarRefreshKey={sidebarRefreshKey} />
    </ChatProvider>
  );
}

/** Contenu du chat — consomme le store (doit être sous le provider). */
function ChatContent({ sidebarRefreshKey }: { sidebarRefreshKey: number }) {
  const { conversationId, selectConversation, newConversation } = useChat();

  const handleSelect = useCallback(
    (id: string | null) => {
      if (id === null) {
        newConversation();
        return;
      }
      selectConversation(id);
    },
    [newConversation, selectConversation],
  );

  return (
    <AppShell
      activeConversationId={conversationId}
      onSelectConversation={handleSelect}
      sidebarRefreshKey={sidebarRefreshKey}
    >
      <div className="flex h-full min-h-0 flex-col">
        <MessageList />
        <ChatInput />
      </div>
    </AppShell>
  );
}
