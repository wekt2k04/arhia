import Link from "next/link";

export default function PageDeGarde() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-slate-50 px-6 text-slate-900">
      <div className="w-full max-w-xl text-center">
        <h1 className="text-4xl font-bold tracking-tight">AGIRH</h1>
        <p className="mt-3 text-lg text-slate-600">
          Assistant RH pour l&apos;onboarding et l&apos;offboarding des collaborateurs.
        </p>
        <p className="mt-6 text-sm text-slate-500">
          Suivez vos checklists d&apos;intégration et de départ, posez vos questions sur les
          politiques internes, et suivez l&apos;avancement de votre dossier via un agent
          conversationnel — toujours sourcé, jamais d&apos;action à votre place.
        </p>

        <div className="mt-10 flex items-center justify-center gap-4">
          <Link
            href="/login"
            className="rounded-md bg-slate-900 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700"
          >
            Se connecter
          </Link>
          <Link
            href="/register"
            className="rounded-md border border-slate-300 px-5 py-2.5 text-sm font-medium text-slate-900 transition hover:bg-slate-100"
          >
            Créer un compte
          </Link>
        </div>
      </div>
    </main>
  );
}
