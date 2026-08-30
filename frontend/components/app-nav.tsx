"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Button } from "@/components/ui/button";
import { estAdminQualite, estRH } from "@/lib/roles";

export function AppNav({ role }: { role: number }) {
  const pathname = usePathname();
  const rhOuAdmin = estRH(role) || estAdminQualite(role);
  const liens = [
    { href: "/dashboard", label: "Vue d'ensemble" },
    { href: "/workflows", label: rhOuAdmin ? "Dossiers" : "Mes dossiers" },
    ...(rhOuAdmin ? [{ href: "/employees", label: "Collaborateurs" }] : []),
    { href: "/chat", label: "Assistant" },
  ];
  return (
    <nav aria-label="Navigation principale" className="hidden items-center gap-1 md:flex">
      {liens.map(({ href, label }) => {
        const actif = pathname === href || pathname.startsWith(`${href}/`);
        return (
          <Button key={href} asChild size="sm" variant={actif ? "secondary" : "ghost"} aria-current={actif ? "page" : undefined}>
            <Link href={href}>{label}</Link>
          </Button>
        );
      })}
    </nav>
  );
}
