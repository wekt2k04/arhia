import Link from "next/link";
import type { ReactNode } from "react";
import { AgirhLogo } from "./agirh-mark";

export function SiteHeader({ nav, right }: { nav?: ReactNode; right?: ReactNode }) {
  return (
    <header className="w-full border-b border-border">
      <div className="mx-auto flex h-16 w-full max-w-5xl items-center justify-between px-6">
        <Link href="/" aria-label="Accueil AGIRH" className="shrink-0">
          <AgirhLogo />
        </Link>
        <div className="flex items-center gap-6">
          {nav}
          {right}
        </div>
      </div>
    </header>
  );
}
