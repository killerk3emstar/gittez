// Ręcznie sklejony adres nie może wysłać do API ani "NaN", ani zera: puste
// maxDifficulty= to dla Number() zero, a zero odsiewa wszystkie issues, bo
// heurystyka zwraca 1-3. Backend odbija to samo, tu chodzi o to, żeby wybór
// pokazany na ekranie zgadzał się z tym, co poleci w zapytaniu.
export function parseMaxDifficulty(raw: string | null): number | null {
  if (raw === null || raw.trim().length === 0) return null

  const value = Number(raw)

  return Number.isInteger(value) && value >= 1 && value <= 3 ? value : null
}
