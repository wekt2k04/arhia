import Link from "next/link";
import type { ReactNode } from "react";
import { ArhiaLogo } from "./arhia-mark";
import { ThemeToggle } from "./theme-toggle";

export function SiteHeader({ nav, right }: { nav?: ReactNode; right?: ReactNode }) {
  return (
    <header className="w-full border-b border-border">
      <div className="mx-auto flex h-20 w-full max-w-screen-2xl items-center gap-6 px-6 md:px-10">
        <Link href="/" aria-label="Accueil arhia" className="shrink-0">
          <ArhiaLogo />
        </Link>

        {/* Bande décorative : occupe l'espace laissé libre entre la marque et la navigation
            plutôt que de rester vide et inerte — dégradé animé aux tons de la marque, avec le
            logo en filigrane. Purement visuel (aria-hidden), jamais au détriment de la nav. */}
        <div
          aria-hidden
          className="header-accent-band relative hidden h-10 flex-1 items-center overflow-hidden rounded-full lg:flex"
        >
          <ArhiaLogo
            className="absolute left-1/2 top-1/2 h-5 w-auto -translate-x-1/2 -translate-y-1/2 opacity-[0.15] grayscale-0"
          />
        </div>

        <div className="flex shrink-0 items-center gap-8">
          {nav}
          <div className="flex items-center gap-4">
            <ThemeToggle />
            {right}
          </div>
        </div>
      </div>
    </header>
  );
}
