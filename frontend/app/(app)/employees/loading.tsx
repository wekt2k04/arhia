import { Skeleton } from "@/components/ui/skeleton";

export default function ChargementEmployees() {
  return (
    <div className="mx-auto max-w-5xl space-y-4 px-6 py-8">
      <div className="flex items-center justify-between">
        <Skeleton className="h-8 w-40" />
        <Skeleton className="h-9 w-40" />
      </div>
      {Array.from({ length: 6 }).map((_, i) => (
        <Skeleton key={i} className="h-14 w-full" />
      ))}
    </div>
  );
}
