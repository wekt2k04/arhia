import { redirect } from "next/navigation";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { SiteHeader } from "@/components/site-header";
import { SiteFooter } from "@/components/site-footer";
import { BoutonDeconnexion } from "./bouton-deconnexion";
import { ChatWidget } from "./chat-widget";
import { NotificationBar } from "./notification-bar";

// RoleType côté Agirh.Api (Agirh.Domain/Enums.cs) : 0=Collaborateur, 1=RH, 2=AdminQualite.
const NOMS_ROLE = ["Collaborateur", "RH", "Admin/Qualité"];

export default async function PageChat() {
  const utilisateur = await obtenirUtilisateurCourant();
  if (!utilisateur) {
    redirect("/login");
  }

  const initiale = utilisateur.email.charAt(0).toUpperCase();

  return (
    <div className="flex min-h-screen flex-col">
      <SiteHeader
        right={
          <div className="flex items-center gap-3">
            <Avatar className="h-9 w-9">
              <AvatarFallback className="bg-accent text-accent-foreground">
                {initiale}
              </AvatarFallback>
            </Avatar>
            <div className="hidden flex-col sm:flex">
              <span className="text-sm font-medium text-foreground">{utilisateur.email}</span>
              <Badge variant="secondary" className="w-fit">
                {NOMS_ROLE[utilisateur.role] ?? utilisateur.role}
              </Badge>
            </div>
            <BoutonDeconnexion />
          </div>
        }
      />

      <main className="flex flex-1 flex-col items-center gap-4 px-6 py-8">
        <NotificationBar />
        <ChatWidget />
      </main>

      <SiteFooter />
    </div>
  );
}
