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
    <div className="rounded-panel border border-dashed border-rule-strong px-6 py-10">
      <h3 className="display text-lg text-ink">{title}</h3>

      {hints.length > 0 && (
        <ul className="mt-4 max-w-lg space-y-2 text-sm text-muted">
          {hints.map((hint) => (
            <li key={hint} className="border-l border-rule-strong pl-3">
              {hint}
            </li>
          ))}
        </ul>
      )}

      {action && <div className="mt-6">{action}</div>}
    </div>
  )
}
