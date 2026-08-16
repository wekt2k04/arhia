import { redirect } from "next/navigation";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
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
    <main className="flex min-h-screen flex-col items-center gap-4 px-6 py-8">
      <div className="flex w-full max-w-2xl items-center justify-between">
        <div className="flex items-center gap-3">
          <Avatar className="h-9 w-9">
            <AvatarFallback className="bg-accent text-accent-foreground">
              {initiale}
            </AvatarFallback>
          </Avatar>
          <div className="flex flex-col">
            <span className="text-sm font-medium text-foreground">{utilisateur.email}</span>
            <Badge variant="secondary" className="w-fit">
              {NOMS_ROLE[utilisateur.role] ?? utilisateur.role}
            </Badge>
          </div>
        </div>
        <BoutonDeconnexion />
      </div>

      <NotificationBar />
      <ChatWidget />
    </main>
  );
}
