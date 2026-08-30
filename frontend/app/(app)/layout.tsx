import { redirect } from "next/navigation";
import { obtenirUtilisateurCourant } from "@/lib/api/current-user";
import { AuthenticatedHeader } from "@/components/authenticated-header";
import { SiteFooter } from "@/components/site-footer";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  const utilisateur = await obtenirUtilisateurCourant();
  if (!utilisateur) redirect("/login");

  return (
    <div className="flex min-h-screen flex-col">
      <AuthenticatedHeader utilisateur={utilisateur} />
      <main className="flex-1">{children}</main>
      <SiteFooter />
    </div>
  );
}
