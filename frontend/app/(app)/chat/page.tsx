import { ChatWidget } from "./chat-widget";

export default function PageChat() {
  return (
    <div className="flex h-full min-h-0 w-full flex-col items-center overflow-hidden px-6 py-6">
      <ChatWidget />
    </div>
  );
}
