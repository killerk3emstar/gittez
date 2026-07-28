import type { ReactNode } from 'react'

type Props = {
  title: string
  // Podpowiedzi z hints[] - brak wyników to poprawny wynik zapytania, więc
  // backend mówi, co poluzować, zamiast zwracać błąd (SPEC §7.3).
  hints?: string[]
  action?: ReactNode
}

export function EmptyState({ title, hints = [], action }: Props) {
  return (
    <div className="rounded-2xl border border-dashed border-ink-700 px-6 py-12 text-center">
      <h3 className="text-lg font-semibold text-white">{title}</h3>

      {hints.length > 0 && (
        <ul className="mx-auto mt-4 max-w-md space-y-1.5 text-sm text-ink-400">
          {hints.map((hint) => (
            <li key={hint}>{hint}</li>
          ))}
        </ul>
      )}

      {action && <div className="mt-6">{action}</div>}
    </div>
  )
}
