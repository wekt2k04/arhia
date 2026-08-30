"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { Department } from "@/lib/api/departments";

const CONTRAT_OPTIONS = [
  { value: 0, label: "CDI" },
  { value: 1, label: "CDD" },
  { value: 2, label: "Stage" },
  { value: 3, label: "Alternance" },
];

export function EmployeeForm({ departments }: { departments: Department[] }) {
  const router = useRouter();
  const [enCours, setEnCours] = useState(false);

  async function soumettre(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setEnCours(true);
    const donnees = new FormData(event.currentTarget);
    const corps = {
      employeeNumber: donnees.get("employeeNumber"),
      lastName: donnees.get("lastName"),
      firstName: donnees.get("firstName"),
      jobTitle: donnees.get("jobTitle"),
      departmentId: donnees.get("departmentId"),
      contractType: Number(donnees.get("contractType")),
      startDate: donnees.get("startDate"),
    };

    const reponse = await fetch("/api/employees", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(corps),
    });
    setEnCours(false);

    if (reponse.ok) {
      const cree = await reponse.json();
      toast.success("Fiche créée. Un onboarding n'est pas encore instancié.");
      router.push(`/employees/${cree.id}`);
    } else {
      const erreur = await reponse.json().catch(() => ({}));
      toast.error(erreur.erreur ?? `Échec (${reponse.status}).`);
    }
  }

  const selectClassName =
    "flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring";

  return (
    <form onSubmit={soumettre} className="space-y-4 max-w-md">
      <div className="space-y-1.5">
        <Label htmlFor="employeeNumber">Matricule</Label>
        <Input id="employeeNumber" name="employeeNumber" required />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="firstName">Prénom</Label>
          <Input id="firstName" name="firstName" required />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="lastName">Nom</Label>
          <Input id="lastName" name="lastName" required />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="jobTitle">Poste</Label>
        <Input id="jobTitle" name="jobTitle" required />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="departmentId">Pôle</Label>
        <select id="departmentId" name="departmentId" required className={selectClassName} defaultValue="">
          <option value="" disabled>Choisir un pôle…</option>
          {departments.map((d) => (
            <option key={d.id} value={d.id}>{d.name}</option>
          ))}
        </select>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="contractType">Type de contrat</Label>
        <select id="contractType" name="contractType" required className={selectClassName} defaultValue={0}>
          {CONTRAT_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="startDate">Date d&apos;intégration</Label>
        <Input id="startDate" name="startDate" type="date" required />
      </div>
      <Button type="submit" disabled={enCours}>Créer la fiche</Button>
    </form>
  );
}
