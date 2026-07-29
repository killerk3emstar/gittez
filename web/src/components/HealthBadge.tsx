import type { ScoreComponent } from '../api/types'

type Props = {
  score: number | null
  breakdown: ScoreComponent[]
}

// Niski wynik dostaje ostrzegawczą plakietkę zamiast wypaść z listy: dziesięć
// kart z wynikiem 80-86 wygląda jak zepsuty licznik, a kontrast jest dowodem,
// że ocena cokolwiek mierzy (SPEC §9).
export function HealthBadge({ score, breakdown }: Props) {
  if (score === null) {
    return (
      <span className="inline-flex items-center gap-1.5 rounded-full border border-ink-700 px-2.5 py-1 text-xs text-ink-400">
        Health: za mało danych
      </span>
    )
  }

  const tone =
    score >= 70
      ? 'border-emerald-500/40 bg-emerald-500/10 text-emerald-300'
      : score >= 45
        ? 'border-amber-500/40 bg-amber-500/10 text-amber-300'
        : 'border-rose-500/40 bg-rose-500/10 text-rose-300'

  const weakest = breakdown
    .filter((c) => c.points !== null)
    .reduce<ScoreComponent | null>(
      (worst, c) => (worst === null || c.points! / c.maxPoints < worst.points! / worst.maxPoints ? c : worst),
      null,
    )

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium ${tone}`}
      title={weakest ? `Najsłabszy komponent: ${weakest.label} - ${weakest.explanation}` : undefined}
    >
      Health {Math.round(score)}
      {score < 45 && weakest && <span className="font-normal opacity-80">- {weakest.rawValue}</span>}
    </span>
  )
}
