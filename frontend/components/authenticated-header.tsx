import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { SiteHeader } from "@/components/site-header";
import { BoutonDeconnexion } from "@/components/bouton-deconnexion";
import { AppNav } from "@/components/app-nav";
import { NotificationBell } from "@/components/notification-bell";
import { nomDuRole } from "@/lib/roles";
import type { UtilisateurCourant } from "@/lib/api/current-user";

export function AuthenticatedHeader({ utilisateur }: { utilisateur: UtilisateurCourant }) {
  const initiale = utilisateur.email.charAt(0).toUpperCase();
  return (
    <SiteHeader
      nav={<AppNav role={utilisateur.role} />}
      right={
        <div className="flex items-center gap-4">
          <NotificationBell />
          <div className="flex items-center gap-3">
            <Avatar className="h-9 w-9">
              <AvatarFallback className="bg-accent text-accent-foreground">{initiale}</AvatarFallback>
            </Avatar>
            <div className="hidden flex-col sm:flex">
              <span className="text-sm font-medium text-foreground">{utilisateur.email}</span>
              <Badge variant="secondary" className="w-fit">{nomDuRole(utilisateur.role)}</Badge>
            </div>
            <BoutonDeconnexion />
          </div>
        </div>
      }
    />
  );
}
