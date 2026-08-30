import { Skeleton } from "@/components/ui/skeleton";

export default function ChargementEmployeeDetail() {
  return (
    <div className="mx-auto max-w-3xl space-y-6 px-6 py-8">
      <Skeleton className="h-8 w-64" />
      <Skeleton className="h-28 w-full" />
      <Skeleton className="h-40 w-full" />
    </div>
  );
}
