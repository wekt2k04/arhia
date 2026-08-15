import { redirect } from "next/navigation";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
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

  return (
    <main className="flex min-h-screen flex-col items-center gap-4 bg-slate-50 px-6 py-8">
      <div className="flex w-full max-w-2xl items-center justify-between">
        <p className="text-sm text-slate-500">
          Connecté en tant que{" "}
          <span className="font-medium text-slate-900">{utilisateur.email}</span> (
          {NOMS_ROLE[utilisateur.role] ?? utilisateur.role})
        </p>
        <BoutonDeconnexion />
      </div>

      <NotificationBar />
      <ChatWidget />
    </main>
  );
}
