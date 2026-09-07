"use client";

import { useEffect, useState } from "react";
import { Bell, X } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

type Notification = {
  type: number;
  message: string;
  referenceDate: string;
  referenceId: string | null;
};

// Arhia.Api.NotificationController pousse un tableau JSON complet (pas d'événement nommé),
// recalculé toutes les 10s (SseNotificationBroadcaster) — pas d'accumulation côté client,
// chaque frame remplace la liste précédente. Placé dans le header (pas seulement sur /chat) pour
// rester visible depuis n'importe quelle page.
export function NotificationBell() {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [connectee, setConnectee] = useState(false);
  const [ouvert, setOuvert] = useState(false);

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

  const nombre = notifications.length;
  const aDesNotifications = connectee && nombre > 0;

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setOuvert((v) => !v)}
        disabled={!aDesNotifications}
        aria-expanded={ouvert}
        aria-label={
          aDesNotifications
            ? `${nombre} notification${nombre > 1 ? "s" : ""}, ${ouvert ? "masquer" : "afficher"} le détail`
            : "Aucune notification"
        }
        className={cn(
          "relative flex h-9 w-9 items-center justify-center rounded-full transition-colors",
          aDesNotifications
            ? "text-amber-700 hover:bg-amber-100 dark:text-amber-300 dark:hover:bg-amber-950"
            : "cursor-default text-muted-foreground",
          ouvert && "bg-amber-100 dark:bg-amber-950",
        )}
      >
        <Bell className="h-5 w-5" aria-hidden />
        {aDesNotifications && (
          <Badge
            variant="default"
            className="absolute -right-1 -top-1 h-5 min-w-5 justify-center rounded-full bg-amber-600 px-1 text-[10px] text-white dark:bg-amber-500"
          >
            {nombre}
          </Badge>
        )}
      </button>

      {ouvert && aDesNotifications && (
        <div
          role="status"
          aria-live="polite"
          className="absolute right-0 top-full z-50 mt-2 w-80 overflow-hidden rounded-lg border border-amber-200 bg-amber-50 shadow-lg dark:border-amber-900 dark:bg-amber-950/95"
        >
          <div className="flex items-center justify-between border-b border-amber-200 px-3 py-2.5 dark:border-amber-900">
            <p className="flex items-center gap-1.5 text-xs font-semibold text-amber-800 dark:text-amber-200">
              <Bell className="h-3.5 w-3.5" aria-hidden />
              {nombre} notification{nombre > 1 ? "s" : ""}
            </p>
            <button
              type="button"
              onClick={() => setOuvert(false)}
              aria-label="Fermer le panneau de notifications"
              className="rounded p-1 text-amber-700 hover:bg-amber-100 dark:text-amber-300 dark:hover:bg-amber-900"
            >
              <X className="h-3.5 w-3.5" aria-hidden />
            </button>
          </div>
          <ul className="max-h-80 space-y-2 overflow-y-auto p-3">
            {notifications.map((notification, index) => (
              <li
                key={notification.referenceId ?? index}
                className="rounded-md bg-amber-100/70 p-2 text-xs text-amber-900 dark:bg-amber-900/40 dark:text-amber-100"
              >
                {notification.message}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
