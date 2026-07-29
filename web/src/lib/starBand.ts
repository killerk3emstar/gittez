// Odpowiednik ScoreMath.StarBand z Core. Pasmo trafia do zapytania do GitHuba,
// nie tylko do wag - przesunięcie suwaka zwraca inne repozytoria (SPEC §0.4).
export function starBand(targetStars: number): { lo: number; hi: number } {
  const lo = Math.max(100, Math.floor(targetStars / 5))

  return { lo, hi: Math.max(lo, targetStars * 5) }
}

// Suwak chodzi po rzędach wielkości, nie liniowo: między 200 a 500 gwiazdek
// jest realna różnica w typie projektu, między 20 000 a 20 300 żadnej.
export const starStops = [200, 500, 1000, 2500, 5000, 10_000, 25_000] as const

export function nearestStopIndex(targetStars: number): number {
  let best = 0

  starStops.forEach((stop, index) => {
    if (Math.abs(Math.log(stop) - Math.log(targetStars)) < Math.abs(Math.log(starStops[best]) - Math.log(targetStars))) {
      best = index
    }
  })

  return best
}
