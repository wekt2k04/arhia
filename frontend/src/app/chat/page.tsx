import type { Metadata } from 'next';

import ChatShell from '@/components/chat/ChatShell';

export const metadata: Metadata = {
  title: 'Chat — AGIRH',
};

/**
 * Page de chat — composant serveur minimal : la logique interactive vit dans
 * ChatShell (client component), qui détient l'état de la conversation active
 * et rend AppShell + Sidebar + la zone centrale.
 * L'authentification est gérée par le middleware (/login si cookie absent).
 */
export default function ChatPage() {
  return <ChatShell />;
}
