import Link from "next/link";
import { ListChecks, MessageSquareText, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";

const ATOUTS = [
  {
    icone: ListChecks,
    titre: "Checklists suivies",
    description: "Onboarding et offboarding, étape par étape, sans rien oublier.",
  },
  {
    icone: MessageSquareText,
    titre: "Agent conversationnel",
    description: "Posez vos questions sur les politiques internes en langage naturel.",
  },
  {
    icone: ShieldCheck,
    titre: "Toujours sourcé",
    description: "Chaque réponse cite ses sources — jamais d'action à votre place.",
  },
];

export default function PageDeGarde() {
  return (
    <main className="relative flex min-h-screen flex-col items-center justify-center overflow-hidden px-6 py-16">
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_50%_0%,hsl(var(--accent))_0%,transparent_60%)]"
      />

      <div className="relative w-full max-w-2xl text-center">
        <span className="inline-flex items-center rounded-full border border-primary/20 bg-accent px-3 py-1 text-xs font-medium text-accent-foreground">
          Assistant RH agentique
        </span>

        <h1 className="mt-5 text-4xl font-bold tracking-tight text-foreground sm:text-5xl">
          AGIRH
        </h1>
        <p className="mt-3 text-lg text-muted-foreground">
          Assistant RH pour l&apos;onboarding et l&apos;offboarding des collaborateurs.
        </p>
        <p className="mx-auto mt-4 max-w-xl text-sm text-muted-foreground">
          Suivez vos checklists d&apos;intégration et de départ, posez vos questions sur les
          politiques internes, et suivez l&apos;avancement de votre dossier via un agent
          conversationnel — toujours sourcé, jamais d&apos;action à votre place.
        </p>

        <div className="mt-10 flex items-center justify-center gap-3">
          <Button asChild size="lg">
            <Link href="/login">Se connecter</Link>
          </Button>
          <Button asChild variant="outline" size="lg">
            <Link href="/register">Créer un compte</Link>
          </Button>
        </div>

        <dl className="mx-auto mt-16 grid gap-6 text-left sm:grid-cols-3">
          {ATOUTS.map(({ icone: Icone, titre, description }) => (
            <div key={titre} className="flex flex-col items-start gap-2">
              <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-accent text-accent-foreground">
                <Icone className="h-5 w-5" aria-hidden />
              </span>
              <dt className="text-sm font-medium text-foreground">{titre}</dt>
              <dd className="text-sm text-muted-foreground">{description}</dd>
            </div>
          ))}
        </dl>
      </div>
    </main>
  );
}
