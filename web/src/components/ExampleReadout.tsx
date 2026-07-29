import { useEffect, useState } from 'react'
import { examples, type ExampleRow } from '../lib/example'
import { RailRow, ScoreRail } from './ScoreRail'

const rotationMs = 7000

// Zamiast obiecywać, pokazujemy gotowy odczyt - razem z komponentem, dla
// którego zabrakło danych. Przykłady zmieniają się same, ale zatrzymują się
// pod kursorem i pod fokusem, milkną na zawsze po ręcznym wyborze i nie ruszają
// w ogóle przy prefers-reduced-motion. Bez tych trzech rzeczy autoodtwarzanie
// jest pułapką, a nie ułatwieniem.
export function ExampleReadout() {
  const [index, setIndex] = useState(0)
  const [hovered, setHovered] = useState(false)
  const [manual, setManual] = useState(false)

  useEffect(() => {
    if (hovered || manual) return
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) return

    const timer = window.setInterval(() => setIndex((current) => (current + 1) % examples.length), rotationMs)

    return () => window.clearInterval(timer)
  }, [hovered, manual])

  const example = examples[index]

  return (
    <figure
      className="rounded-panel border border-rule bg-panel p-5"
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onFocusCapture={() => setHovered(true)}
      onBlurCapture={() => setHovered(false)}
    >
      <figcaption className="flex items-baseline justify-between gap-3">
        <span className="label text-muted">Przykład odczytu</span>
        <span className="font-mono text-xs text-muted">{example.fullName}</span>
      </figcaption>

      <div key={example.fullName} className="animate-fade-in mt-5 space-y-6">
        <Axis title="Match Score" axis="match" score={example.matchScore} rows={example.match} />
        <Axis title="Health Score" axis="health" score={example.healthScore} rows={example.health} />
      </div>

      <div className="mt-6 border-t border-rule pt-4">
        <div className="flex gap-1.5" role="tablist" aria-label="Przykłady odczytu">
          {examples.map((item, position) => (
            <button
              key={item.fullName}
              type="button"
              role="tab"
              aria-selected={position === index}
              aria-label={`Przykład ${position + 1} z ${examples.length}: ${item.fullName}`}
              onClick={() => {
                setIndex(position)
                setManual(true)
              }}
              className={`h-1.5 rounded-full transition ${
                position === index ? 'w-6 bg-ink-soft' : 'w-1.5 bg-track hover:bg-rule-strong'
              }`}
            />
          ))}
        </div>

        <p className="mt-3 text-xs leading-relaxed text-muted">
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
}: {
  title: string
  axis: 'match' | 'health'
  score: number
  rows: ExampleRow[]
}) {
  return (
    <div>
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
