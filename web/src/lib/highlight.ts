import type { Recommendation, ScoreComponent } from '../api/types'

// Karta nie pokazuje wyniku końcowego, tylko jedno zdanie z komponentu, który
// najmocniej wyróżnia to repo na tle pozostałych (SPEC §9). "Najmocniej"
// liczymy jako odchylenie od średniej dla tego komponentu w widocznej
// dziesiątce - komponent, który wszystkim daje tyle samo, nic nie mówi.
export function pickHighlights(items: Recommendation[]): Map<string, ScoreComponent> {
  const ratios = new Map<string, number[]>()

  for (const item of items) {
    for (const component of allComponents(item)) {
      if (component.points === null) continue
      const bucket = ratios.get(component.key) ?? []
      bucket.push(component.points / component.maxPoints)
      ratios.set(component.key, bucket)
    }
  }

  const averages = new Map<string, number>()
  for (const [key, values] of ratios) {
    averages.set(key, values.reduce((sum, v) => sum + v, 0) / values.length)
  }

  const highlights = new Map<string, ScoreComponent>()

  for (const item of items) {
    let best: ScoreComponent | null = null
    let bestDeviation = -Infinity

    for (const component of allComponents(item)) {
      if (component.points === null) continue

      const ratio = component.points / component.maxPoints
      const deviation = ratio - (averages.get(component.key) ?? ratio)

      if (deviation > bestDeviation) {
        best = component
        bestDeviation = deviation
      }
    }

    if (best) highlights.set(item.fullName, best)
  }

  return highlights
}

function allComponents(item: Recommendation): ScoreComponent[] {
  return [...item.matchBreakdown, ...item.healthBreakdown]
}

export type Band = { lo: number; hi: number }

// Skala na karcie pokazuje też, gdzie wylądowała reszta widocznej dziesiątki.
// Wynik 78 znaczy co innego, gdy wszyscy siedzą w 74-81, a co innego, gdy
// pole rozciąga się od 20 do 90 - sama liczba tego nie powie.
export function scoreBands(items: Recommendation[]): { match: Band | null; health: Band | null } {
  return {
    match: band(items.map((item) => item.matchScore)),
    health: band(items.map((item) => item.healthScore)),
  }
}

function band(values: Array<number | null>): Band | null {
  const known = values.filter((value): value is number => value !== null)
  if (known.length < 3) return null

  const lo = Math.min(...known)
  const hi = Math.max(...known)

  // Pole węższe niż punkt nie niesie informacji, tylko brudzi tor.
  return hi - lo < 1 ? null : { lo, hi }
}

// Najsłabszy komponent zdrowia - kontrast jest dowodem, że ocena cokolwiek
// mierzy, więc niski wynik mówi od razu, na czym poległ (SPEC §9).
export function weakestComponent(components: ScoreComponent[]): ScoreComponent | null {
  return components
    .filter((component) => component.points !== null)
    .reduce<ScoreComponent | null>(
      (worst, component) =>
        worst === null || component.points! / component.maxPoints < worst.points! / worst.maxPoints
          ? component
          : worst,
      null,
    )
}
