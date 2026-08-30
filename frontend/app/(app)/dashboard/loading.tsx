import { Skeleton } from "@/components/ui/skeleton";

export default function ChargementDashboard() {
  return (
    <div className="mx-auto max-w-5xl space-y-6 px-6 py-8">
      <Skeleton className="h-8 w-48" />
      <div className="grid gap-4 sm:grid-cols-3">
        <Skeleton className="h-24" />
        <Skeleton className="h-24" />
        <Skeleton className="h-24" />
      </div>
      <Skeleton className="h-64" />
    </div>
  );
}
