// Pierwszy przebieg dla nowego użytkownika to 6-8 sekund, bo GitHub dławi
// współbieżność (SPEC §2.2) - skeleton nie jest tu ozdobnikiem.
export function CardSkeleton() {
  return (
    <div className="animate-pulse-soft rounded-2xl border border-ink-800 bg-ink-900/50 p-5">
      <div className="flex items-start gap-4">
        <div className="flex-1 space-y-2">
          <div className="h-4 w-2/5 rounded bg-ink-800" />
          <div className="h-3 w-full rounded bg-ink-800" />
          <div className="h-3 w-3/4 rounded bg-ink-800" />
        </div>
        <div className="size-16 shrink-0 rounded-full bg-ink-800" />
      </div>
      <div className="mt-5 space-y-2">
        <div className="h-9 w-full rounded-lg bg-ink-800" />
        <div className="h-9 w-full rounded-lg bg-ink-800" />
      </div>
    </div>
  )
}

export function ResultsSkeleton({ count = 6 }: { count?: number }) {
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      {Array.from({ length: count }, (_, i) => (
        <CardSkeleton key={i} />
      ))}
    </div>
  )
}

export function LineSkeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse-soft rounded bg-ink-800 ${className}`} />
}
