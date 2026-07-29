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
