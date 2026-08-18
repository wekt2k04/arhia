"use client";

import { useId, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { SiteHeader } from "@/components/site-header";
import { SiteFooter } from "@/components/site-footer";
import { AuthNav } from "@/components/auth-nav";

export default function PageInscription() {
  const router = useRouter();
  const emailId = useId();
  const passwordId = useId();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [erreur, setErreur] = useState<string | null>(null);
  const [enCours, setEnCours] = useState(false);

  async function soumettre(evenement: FormEvent) {
    evenement.preventDefault();
    setErreur(null);
    setEnCours(true);

    try {
      const reponse = await fetch("/api/auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });

      if (!reponse.ok) {
        const donnees = await reponse.json().catch(() => null);
        setErreur(donnees?.erreur ?? "Impossible de créer le compte.");
        return;
      }

      router.push("/chat");
      router.refresh();
    } finally {
      setEnCours(false);
    }
  }

  return (
    <div className="flex min-h-screen flex-col">
      <SiteHeader nav={<AuthNav />} />
      <main className="flex flex-1 items-center justify-center px-6 py-12">
      <Card className="w-full max-w-sm">
        <form onSubmit={soumettre} noValidate>
          <CardHeader>
            <CardTitle className="text-xl">Créer un compte</CardTitle>
            <CardDescription>
              Réservé aux nouveaux collaborateurs. Votre compte est créé avec un accès
              collaborateur ; le RH de votre pôle complètera votre fiche.
            </CardDescription>
          </CardHeader>

          <CardContent className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor={emailId}>Email</Label>
              <Input
                id={emailId}
                type="email"
                required
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor={passwordId}>Mot de passe</Label>
              <Input
                id={passwordId}
                type="password"
                required
                autoComplete="new-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </div>

            {erreur && (
              <Alert variant="destructive">
                <AlertDescription>{erreur}</AlertDescription>
              </Alert>
            )}
          </CardContent>

          <CardFooter className="flex flex-col gap-4">
            <Button type="submit" disabled={enCours} className="w-full">
              {enCours && <Loader2 className="animate-spin" aria-hidden />}
              {enCours ? "Création..." : "Créer mon compte"}
            </Button>

            <p className="text-center text-sm text-muted-foreground">
              Déjà un compte ?{" "}
              <Link href="/login" className="font-medium text-primary underline underline-offset-4">
                Se connecter
              </Link>
            </p>
          </CardFooter>
        </form>
      </Card>
      </main>
      <SiteFooter />
    </div>
  );
}
