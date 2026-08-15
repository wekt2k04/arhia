"use client";

import { useEffect, useState } from "react";

type Notification = {
  type: number;
  message: string;
  dateReference: string;
  referenceId: string | null;
};

// Agirh.Api.NotificationController pousse un tableau JSON complet (pas d'événement nommé),
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
    <div className="w-full max-w-2xl rounded-lg border border-amber-200 bg-amber-50 p-3">
      <p className="text-xs font-semibold text-amber-800">
        {notifications.length} notification{notifications.length > 1 ? "s" : ""}
      </p>
      <ul className="mt-1 space-y-1">
        {notifications.map((notification, index) => (
          <li key={notification.referenceId ?? index} className="text-xs text-amber-900">
            {notification.message}
          </li>
        ))}
      </ul>
    </div>
  );
}
