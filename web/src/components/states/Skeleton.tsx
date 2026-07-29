// Pierwszy przebieg dla nowego użytkownika to 6-8 sekund, bo GitHub dławi
// współbieżność (SPEC §2.2) - skeleton nie jest tu ozdobnikiem.
export function CardSkeleton() {
  return (
    <div className="animate-pulse-soft rounded-panel border border-rule bg-panel p-5">
      <div className="flex items-start gap-3">
        <div className="flex-1 space-y-2">
          <div className="h-3.5 w-2/5 rounded-chip bg-track" />
          <div className="h-2.5 w-3/5 rounded-chip bg-track" />
        </div>
        <div className="size-7 shrink-0 rounded-chip bg-track" />
      </div>

      <div className="mt-5 space-y-4">
        <div className="space-y-1.5">
          <div className="h-2 w-16 rounded-chip bg-track" />
          <div className="h-1.5 w-full rounded-[1px] bg-track" />
        </div>
        <div className="space-y-1.5">
          <div className="h-2 w-16 rounded-chip bg-track" />
          <div className="h-1.5 w-full rounded-[1px] bg-track" />
        </div>
      </div>

      <div className="mt-5 space-y-2 border-t border-rule pt-4">
        <div className="h-3 w-4/5 rounded-chip bg-track" />
        <div className="h-3 w-3/5 rounded-chip bg-track" />
      </div>
    </div>
  )
}

export function ResultsSkeleton({ count = 6 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      {Array.from({ length: count }, (_, i) => (
        <CardSkeleton key={i} />
      ))}
    </div>
  )
}

// Bez zaokrąglenia w bazie: element ma udawać to, co się ładuje, więc promień
// podaje wywołujący. Wpisany na sztywno zderzałby się z nim dwiema klasami o
// tej samej właściwości i wygrywałaby kolejność w arkuszu, nie intencja.
export function LineSkeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse-soft bg-track ${className}`} />
}
