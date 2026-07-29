import type { ReactNode } from 'react'

// Kolor w tym interfejsie niesie wyłącznie dane: miedź to oś dopasowania,
// patyna oś zdrowia, bursztyn i rdza to stan ostrzegawczy. Chrome - przyciski,
// nawigacja, ramki - zostaje monochromatyczna.
export type Axis = 'match' | 'health' | 'neutral' | 'warn' | 'danger'

const axisColor: Record<Axis, string> = {
  match: 'var(--copper)',
  health: 'var(--patina)',
  neutral: 'var(--ink-soft)',
  warn: 'var(--amber)',
  danger: 'var(--rust)',
}

const axisText: Record<Axis, string> = {
  match: 'text-copper-ink',
  health: 'text-patina-ink',
  neutral: 'text-ink',
  warn: 'text-amber',
  danger: 'text-rust',
}

type RailProps = {
  value: number | null
  max?: number
  axis?: Axis
  // Rozrzut tej samej miary w widocznej dziesiątce. Wynik 78 znaczy co innego,
  // gdy reszta siedzi w 70-80, a co innego, gdy rozjeżdża się od 20 do 90.
  band?: { lo: number; hi: number } | null
  size?: 'sm' | 'md'
  label?: string
}

function percent(value: number, max: number): number {
  return Math.max(0, Math.min(100, (value / max) * 100))
}

export function ScoreRail({ value, max = 100, axis = 'neutral', band = null, size = 'md', label }: RailProps) {
  const color = axisColor[axis]
  const height = size === 'sm' ? 'h-1' : 'h-1.5'

  if (value === null) {
    return (
      <div
        className={`relative w-full overflow-hidden rounded-[1px] bg-track ${height}`}
        role="img"
        aria-label={label ? `${label}: za mało danych` : 'za mało danych'}
      >
        <div className="rail-missing absolute inset-0 opacity-70" />
      </div>
    )
  }

  const fill = percent(value, max)

  return (
    <div
      className={`relative w-full rounded-[1px] bg-track ${height}`}
      role="img"
      aria-label={label ? `${label}: ${value.toFixed(1)} na ${max}` : `${value.toFixed(1)} na ${max}`}
    >
      <div className="rail-ticks absolute inset-0 opacity-50" aria-hidden="true" />

      {band && (
        <div
          className="absolute inset-y-0"
          aria-hidden="true"
          style={{
            left: `${percent(band.lo, max)}%`,
            width: `${percent(band.hi, max) - percent(band.lo, max)}%`,
            background: color,
            opacity: 0.22,
          }}
        />
      )}

      <div
        className="absolute inset-y-0 left-0"
        aria-hidden="true"
        style={{ width: `${fill}%`, background: color, opacity: 0.45 }}
      />

      <div
        className="absolute -top-1 -bottom-1 w-0.5"
        aria-hidden="true"
        style={{ left: `${fill}%`, marginLeft: -1, background: color }}
      />
    </div>
  )
}

type RowProps = RailProps & {
  label: string
  // Wartość surowa zamiast liczby punktów - "96% PR-ów zmergowanych" mówi
  // więcej niż "24 / 25", więc oba miejsca w wierszu są zajęte treścią.
  readout: ReactNode
  note?: ReactNode
}

// Wiersz odczytu: etykieta, wartość, skala, przypis. Ten sam układ w karcie,
// w rozbiciu i przy limicie API - jeden przyrząd w trzech rozmiarach.
export function RailRow({ label, readout, note, ...rail }: RowProps) {
  // Brak danych nie dostaje koloru osi: "za mało danych" wyróżnione miedzią
  // czytałoby się jak wynik.
  const tone = rail.value === null ? 'text-muted' : axisText[rail.axis ?? 'neutral']

  return (
    <div>
      <div className="flex items-baseline justify-between gap-3">
        <span className="label text-muted">{label}</span>
        <span className={`num shrink-0 text-sm font-medium ${tone}`}>{readout}</span>
      </div>

      <div className="mt-1.5">
        <ScoreRail {...rail} label={label} />
      </div>

      {note && <p className="mt-1.5 text-xs leading-snug text-muted">{note}</p>}
    </div>
  )
}
