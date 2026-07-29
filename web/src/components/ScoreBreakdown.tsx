import { useEffect, useRef } from 'react'
import type { Recommendation, ScoreComponent } from '../api/types'
import { RailRow, ScoreRail, type Axis } from './ScoreRail'

type Props = {
  item: Recommendation
  onClose: () => void
}

// Serce projektu: dwie listy odczytów, każdy z liczbą, wartością źródłową i
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

    // Tło pod modalem nie ma się przewijać razem z nim.
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = previousOverflow
    }
  }, [onClose])

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-scrim p-0 backdrop-blur-sm sm:p-8"
      onClick={onClose}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-label={`Rozbicie oceny: ${item.fullName}`}
        tabIndex={-1}
        className="w-full max-w-3xl border-rule bg-panel shadow-xl outline-none sm:rounded-panel sm:border"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="flex items-start justify-between gap-4 border-b border-rule px-5 py-4 sm:px-6 sm:py-5">
          <div className="min-w-0">
            <h2 className="truncate font-mono text-base font-medium text-ink">{item.fullName}</h2>
            <p className="mt-1 text-sm text-muted">
              Każdy komponent niesie własne wyjaśnienie - nic tu nie jest czarną skrzynką.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="shrink-0 rounded-chip border border-rule px-3 py-1.5 text-sm text-ink transition hover:border-rule-strong hover:bg-sunk"
          >
            Zamknij
          </button>
        </header>

        <div className="grid gap-10 px-5 py-6 sm:px-6 md:grid-cols-2">
          <Section
            title="Match Score"
            subtitle="jak bardzo repo pasuje do Ciebie"
            score={item.matchScore}
            components={item.matchBreakdown}
            axis="match"
          />
          <Section
            title="Health Score"
            subtitle="czy repo żyje, niezależnie od tego, kim jesteś"
            score={item.healthScore}
            components={item.healthBreakdown}
            axis="health"
          />
        </div>

        <footer className="border-t border-rule px-5 py-4 text-xs leading-relaxed text-muted sm:px-6">
          Komponenty bez danych nie są liczone jako zero - wynik procentuje się po dostępnych. Wynik końcowy (
          <span className="num">{item.finalScore.toFixed(1)}</span>) służy wyłącznie do ustalenia kolejności listy.
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
  axis: Axis
}

function Section({ title, subtitle, score, components, axis }: SectionProps) {
  const tone = axis === 'match' ? 'text-copper-ink' : 'text-patina-ink'

  return (
    <section>
      <div className="flex items-baseline justify-between gap-3">
        <h3 className="display text-base text-ink">{title}</h3>
        <span className={`display num text-2xl ${tone}`}>{score === null ? '-' : score.toFixed(1)}</span>
      </div>
      <p className="mt-0.5 text-xs text-muted">{subtitle}</p>

      <div className="mt-3">
        <ScoreRail value={score} axis={axis} label={title} />
      </div>

      {components.length === 0 ? (
        <p className="mt-6 border border-rule px-3 py-4 text-sm text-muted">Brak danych do policzenia tej oceny.</p>
      ) : (
        <ul className="mt-6 space-y-5">
          {components.map((component) => (
            <li key={component.key}>
              <RailRow
                label={component.label}
                axis={axis}
                value={component.points}
                max={component.maxPoints}
                readout={
                  component.points === null
                    ? 'za mało danych'
                    : `${component.points.toFixed(1)} / ${component.maxPoints}`
                }
                note={
                  <>
                    <span className="text-ink-soft">{component.rawValue}</span>
                    {' - '}
                    {component.explanation}
                    {component.isSampled && (
                      <span className="text-amber" title="Liczone na próbce, nie na całości">
                        {' '}
                        (próbka)
                      </span>
                    )}
                  </>
                }
              />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
