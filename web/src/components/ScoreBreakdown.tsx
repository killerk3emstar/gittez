import { useEffect, useRef } from 'react'
import type { Recommendation, ScoreComponent } from '../api/types'

type Props = {
  item: Recommendation
  onClose: () => void
}

// Serce projektu: dwie listy pasków, każdy z liczbą, wartością źródłową i
// zdaniem wyjaśnienia. Zdanie przychodzi z ScoreComponent.Explanation, więc
// opis nie może rozjechać się z wartością (SPEC §9).
export function ScoreBreakdown({ item, onClose }: Props) {
  const dialogRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }

    document.addEventListener('keydown', onKey)
    dialogRef.current?.focus()

    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/70 p-4 sm:p-8"
      onClick={onClose}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-label={`Rozbicie oceny: ${item.fullName}`}
        tabIndex={-1}
        className="w-full max-w-3xl rounded-2xl border border-ink-800 bg-ink-900 shadow-2xl outline-none"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="flex items-start justify-between gap-4 border-b border-ink-800 px-6 py-5">
          <div className="min-w-0">
            <h2 className="truncate text-lg font-semibold text-white">{item.fullName}</h2>
            <p className="mt-1 text-sm text-ink-400">
              Każdy komponent niesie własne wyjaśnienie - nic tu nie jest czarną skrzynką.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="shrink-0 rounded-lg border border-ink-700 px-3 py-1.5 text-sm text-ink-200 transition hover:bg-ink-800"
          >
            Zamknij
          </button>
        </header>

        <div className="grid gap-8 px-6 py-6 md:grid-cols-2">
          <Section
            title="Match Score"
            subtitle="jak bardzo repo pasuje do Ciebie"
            score={item.matchScore}
            components={item.matchBreakdown}
            tone="text-sky-400"
          />
          <Section
            title="Health Score"
            subtitle="czy repo żyje, niezależnie od tego, kim jesteś"
            score={item.healthScore}
            components={item.healthBreakdown}
            tone="text-emerald-400"
          />
        </div>

        <footer className="border-t border-ink-800 px-6 py-4 text-xs text-ink-400">
          Komponenty bez danych nie są liczone jako zero - wynik procentuje się po dostępnych. Wynik końcowy (
          {item.finalScore.toFixed(1)}) służy wyłącznie do ustalenia kolejności listy.
        </footer>
      </div>
    </div>
  )
}

type SectionProps = {
  title: string
  subtitle: string
  score: number | null
  components: ScoreComponent[]
  tone: string
}

function Section({ title, subtitle, score, components, tone }: SectionProps) {
  return (
    <section>
      <div className="mb-4 flex items-baseline justify-between gap-3">
        <div>
          <h3 className="font-semibold text-white">{title}</h3>
          <p className="text-xs text-ink-400">{subtitle}</p>
        </div>
        <span className={`text-2xl font-semibold tabular-nums ${tone}`}>
          {score === null ? '-' : score.toFixed(1)}
        </span>
      </div>

      {components.length === 0 ? (
        <p className="rounded-lg border border-ink-800 px-3 py-4 text-sm text-ink-400">
          Brak danych do policzenia tej oceny.
        </p>
      ) : (
        <ul className="space-y-4">
          {components.map((component) => (
            <ComponentBar key={component.key} component={component} tone={tone} />
          ))}
        </ul>
      )}
    </section>
  )
}

function ComponentBar({ component, tone }: { component: ScoreComponent; tone: string }) {
  const missing = component.points === null
  const ratio = missing ? 0 : component.points! / component.maxPoints

  return (
    <li>
      <div className="flex items-baseline justify-between gap-3 text-sm">
        <span className={missing ? 'text-ink-400' : 'text-ink-200'}>{component.label}</span>
        <span className={`shrink-0 tabular-nums ${missing ? 'text-ink-400' : 'text-white'}`}>
          {missing ? 'za mało danych' : `${component.points!.toFixed(1)} / ${component.maxPoints}`}
        </span>
      </div>

      <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-ink-800">
        {!missing && (
          <div
            className={`h-full rounded-full bg-current ${tone}`}
            style={{ width: `${Math.max(2, ratio * 100)}%` }}
          />
        )}
      </div>

      <p className="mt-1.5 text-xs text-ink-400">
        <span className="text-ink-200">{component.rawValue}</span>
        {' - '}
        {component.explanation}
        {component.isSampled && (
          <span className="ml-1 text-amber-400/80" title="Liczone na próbce, nie na całości">
            (próbka)
          </span>
        )}
      </p>
    </li>
  )
}
