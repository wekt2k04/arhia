"use client";

import { useEffect, useState } from "react";
import { Bell } from "lucide-react";
import { cn } from "@/lib/utils";

type Notification = {
  type: number;
  message: string;
  referenceDate: string;
  referenceId: string | null;
};

// Arhia.Api.NotificationController pousse un tableau JSON complet (pas d'événement nommé),
// recalculé toutes les 10s (SseNotificationBroadcaster) — pas d'accumulation côté client,
// chaque frame remplace la liste précédente.
export function NotificationBar() {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [connectee, setConnectee] = useState(false);

  useEffect(() => {
    const source = new EventSource("/api/notifications/stream");

    source.onopen = () => setConnectee(true);

    source.onmessage = (evt) => {
      try {
        setNotifications(JSON.parse(evt.data) as Notification[]);
      } catch {
        // frame malformée ignorée, on garde le dernier état connu
      }
    };

    source.onerror = () => setConnectee(false);

    return () => source.close();
  }, []);

  if (!connectee || notifications.length === 0) {
    return null;
  }

  return (
    <div
      role="status"
      aria-live="polite"
      className={cn(
        "w-full max-w-2xl rounded-lg border border-amber-300/60 bg-amber-50 p-3 dark:border-amber-900 dark:bg-amber-950",
      )}
    >
      <p className="flex items-center gap-1.5 text-xs font-semibold text-amber-800 dark:text-amber-200">
        <Bell className="h-3.5 w-3.5" aria-hidden />
        {notifications.length} notification{notifications.length > 1 ? "s" : ""}
      </p>
      <ul className="mt-1.5 space-y-1">
        {notifications.map((notification, index) => (
          <li
            key={notification.referenceId ?? index}
            className="text-xs text-amber-900 dark:text-amber-100"
          >
            {notification.message}
          </li>
        ))}
      </ul>
    </div>
  );
}
