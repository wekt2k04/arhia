import { redirect } from 'next/navigation';

/**
 * Page d'accueil : redirige immédiatement vers le chat.
 * Le middleware (src/app/middleware.ts) redirige ensuite vers /login
 * si le cookie de session est absent.
 */
export default function Home() {
  redirect('/chat');
}
