import { useEffect, useState } from 'react'
import { examples, type ExampleRow } from '../lib/example'
import { RailRow, ScoreRail } from './ScoreRail'

const rotationMs = 5000

// Zamiast obiecywać, pokazujemy gotowy odczyt - razem z komponentem, dla
// którego zabrakło danych.
//
// Przykład przełącza koniec animacji paska w aktywnej kropce, nie osobny
// licznik czasu: pasek pokazuje dokładnie to, co steruje zmianą, więc pauza
// pod kursorem zatrzyma jedno i drugie bez synchronizowania dwóch zegarów.
export function ExampleReadout() {
  const [index, setIndex] = useState(0)
  const [paused, setPaused] = useState(false)
  const [manual, setManual] = useState(false)

  // Startujemy bez ruchu i włączamy go dopiero, gdy wiemy, że wolno: przy
  // prefers-reduced-motion animacja trwa 0,01 ms i przewinęłaby wszystkie
  // przykłady w jednej klatce.
  const [rotating, setRotating] = useState(false)

  useEffect(() => {
    const query = matchMedia('(prefers-reduced-motion: reduce)')
    const sync = () => setRotating(!query.matches)

    sync()
    query.addEventListener('change', sync)

    return () => query.removeEventListener('change', sync)
  }, [])

  const example = examples[index]
  const running = rotating && !manual

  const advance = () => setIndex((current) => (current + 1) % examples.length)

  return (
    <figure
      className="rounded-panel border border-rule bg-panel p-5"
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onFocusCapture={() => setPaused(true)}
      onBlurCapture={() => setPaused(false)}
    >
      <figcaption className="flex items-baseline justify-between gap-3">
        <span className="label text-muted">Przykład odczytu</span>
        <span key={example.fullName} className="animate-readout-in font-mono text-xs text-muted">
          {example.fullName}
        </span>
      </figcaption>

      <div className="mt-5 space-y-6">
        <Axis
          key={`${example.fullName}-match`}
          title="Match Score"
          axis="match"
          score={example.matchScore}
          rows={example.match}
          delayMs={0}
        />
        <Axis
          key={`${example.fullName}-health`}
          title="Health Score"
          axis="health"
          score={example.healthScore}
          rows={example.health}
          delayMs={90}
        />
      </div>

      <div className="mt-6 border-t border-rule pt-2">
        {/* Nie role="tablist": wzorzec zakładek obiecuje nawigację strzałkami i
            powiązany panel, a to są wskaźniki karuzeli. aria-current mówi
            prawdę o tym, co tu faktycznie działa. */}
        <div className="flex items-center gap-2" role="group" aria-label="Przykłady odczytu">
          {examples.map((item, position) => {
            const active = position === index

            return (
              <button
                key={item.fullName}
                type="button"
                aria-current={active}
                aria-label={`Przykład ${position + 1} z ${examples.length}: ${item.fullName}`}
                onClick={() => {
                  setIndex(position)
                  setManual(true)
                }}
                // Pasek ma 6 px wysokości, więc obszar kliknięcia robi padding -
                // sam wskaźnik byłby celem nie do trafienia na telefonie.
                className="group py-2"
              >
                <span
                  className={`block h-1.5 overflow-hidden rounded-full bg-track transition-all duration-300 ease-out ${
                    active ? 'w-10' : 'w-1.5 group-hover:bg-rule-strong'
                  }`}
                >
                  {active &&
                    (running ? (
                      <span
                        key={index}
                        onAnimationEnd={advance}
                        style={{
                          animationDuration: `${rotationMs}ms`,
                          animationPlayState: paused ? 'paused' : 'running',
                        }}
                        className="animate-rail-fill block h-full w-full origin-left rounded-full bg-ink-soft"
                      />
                    ) : (
                      <span className="block h-full w-full rounded-full bg-ink-soft" />
                    ))}
                </span>
              </button>
            )
          })}
        </div>

        <p className="mt-2 text-xs leading-relaxed text-muted">
          Wartości są przykładowe, nie odczytem na żywo. Przy prawdziwych wynikach to samo rozbicie otwiera się
          pod „Skąd te liczby?" na każdej karcie.
        </p>
      </div>
    </figure>
  )
}

function Axis({
  title,
  axis,
  score,
  rows,
  delayMs,
}: {
  title: string
  axis: 'match' | 'health'
  score: number
  rows: ExampleRow[]
  delayMs: number
}) {
  return (
    <div className="animate-readout-in" style={{ animationDelay: `${delayMs}ms` }}>
      <div className="flex items-baseline justify-between gap-3">
        <h3 className="display text-sm text-ink">{title}</h3>
        <span className={`display num text-xl ${axis === 'match' ? 'text-copper-ink' : 'text-patina-ink'}`}>
          {score.toFixed(1)}
        </span>
      </div>

      <div className="mt-2">
        <ScoreRail value={score} axis={axis} label={title} />
      </div>

      <ul className="mt-4 space-y-3">
        {rows.map((row) => (
          <li key={row.label}>
            <RailRow
              label={row.label}
              axis={axis}
              size="sm"
              value={row.points}
              max={row.maxPoints}
              readout={row.points === null ? 'za mało danych' : `${row.points.toFixed(1)} / ${row.maxPoints}`}
              note={row.readout}
            />
          </li>
        ))}
      </ul>
    </div>
  )
}
