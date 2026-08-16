"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Button } from "@/components/ui/button";

const LIENS = [
  { href: "/login", label: "Se connecter" },
  { href: "/register", label: "Créer un compte" },
];

export function AuthNav() {
  const pathname = usePathname();

  return (
    <nav aria-label="Authentification" className="flex items-center gap-2">
      {LIENS.map(({ href, label }) => {
        const actif = pathname === href;
        return (
          <Button
            key={href}
            asChild
            size="sm"
            variant={actif ? "default" : "ghost"}
            aria-current={actif ? "page" : undefined}
          >
            <Link href={href}>{label}</Link>
          </Button>
        );
      })}
    </nav>
  );
}
