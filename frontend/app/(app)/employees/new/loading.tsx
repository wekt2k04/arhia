import { Skeleton } from "@/components/ui/skeleton";

export default function ChargementNouveauEmployee() {
  return (
    <div className="mx-auto max-w-3xl space-y-4 px-6 py-8">
      <Skeleton className="h-8 w-56" />
      <Skeleton className="h-96 w-full max-w-md" />
    </div>
  );
}
