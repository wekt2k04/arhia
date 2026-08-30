import { ChatWidget } from "./chat-widget";
import { NotificationBar } from "./notification-bar";

export default function PageChat() {
  return (
    <main className="flex flex-1 flex-col items-center gap-4 px-6 py-8">
      <NotificationBar />
      <ChatWidget />
    </main>
  );
}
