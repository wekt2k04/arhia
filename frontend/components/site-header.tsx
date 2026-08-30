import Link from "next/link";
import type { ReactNode } from "react";
import { ArhiaLogo } from "./arhia-mark";
import { ThemeToggle } from "./theme-toggle";

export function SiteHeader({ nav, right }: { nav?: ReactNode; right?: ReactNode }) {
  return (
    <header className="w-full border-b border-border">
      <div className="mx-auto flex h-16 w-full max-w-5xl items-center justify-between px-6">
        <Link href="/" aria-label="Accueil arhia" className="shrink-0">
          <ArhiaLogo />
        </Link>
        <div className="flex items-center gap-6">
          {nav}
          <div className="flex items-center gap-2">
            <ThemeToggle />
            {right}
          </div>
        </div>
      </div>
    </header>
  );
}
