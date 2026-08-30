import { Skeleton } from "@/components/ui/skeleton";

export default function ChargementWorkflows() {
  return (
    <div className="mx-auto max-w-5xl space-y-4 px-6 py-8">
      <Skeleton className="h-8 w-32" />
      <Skeleton className="h-10 w-full" />
      {Array.from({ length: 6 }).map((_, i) => (
        <Skeleton key={i} className="h-14 w-full" />
      ))}
    </div>
  );
}
