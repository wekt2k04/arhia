import { redirect } from "next/navigation";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
import { BoutonDeconnexion } from "./bouton-deconnexion";

// RoleType côté Agirh.Api (Agirh.Domain/Enums.cs) : 0=Collaborateur, 1=RH, 2=AdminQualite.
const NOMS_ROLE = ["Collaborateur", "RH", "Admin/Qualité"];

export default async function PageChat() {
  const utilisateur = await obtenirUtilisateurCourant();
  if (!utilisateur) {
    redirect("/login");
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-slate-50 px-6 text-center">
      <p className="text-sm text-slate-500">
        Connecté en tant que{" "}
        <span className="font-medium text-slate-900">{utilisateur.email}</span> (
        {NOMS_ROLE[utilisateur.role] ?? utilisateur.role})
      </p>
      <h1 className="mt-2 text-2xl font-semibold text-slate-900">Chat AGIRH — bientôt disponible</h1>
      <p className="mt-2 max-w-md text-sm text-slate-500">
        L&apos;interface de conversation avec l&apos;agent (streaming en temps réel et barre de
        notifications) arrive dans une prochaine étape. Le flux de connexion, lui, est
        opérationnel : vous êtes bien authentifié via un cookie httpOnly.
      </p>
      <div className="mt-6">
        <BoutonDeconnexion />
      </div>
    </main>
  );
}
