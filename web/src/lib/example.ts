// Przykład na stronie głównej: pokazujemy produkt tym, co go odróżnia, czyli
// rozbiciem oceny. Wartości są ilustracją, nie odczytem - strona mówi to
// wprost obok, a nie w przypisie.
export type ExampleRow = {
  label: string
  points: number | null
  maxPoints: number
  readout: string
}

export const exampleRepo = {
  fullName: 'MudBlazor/MudBlazor',
  matchScore: 78.5,
  healthScore: 84,
  match: [
    { label: 'Dopasowanie języka', points: 30, maxPoints: 30, readout: 'C# - miejsce 1 w Twoim rankingu' },
    { label: 'Przystępność kodu', points: 19.5, maxPoints: 25, readout: 'mniejsze niż 78% kandydatów' },
    { label: 'Dopasowanie tematyki', points: null, maxPoints: 25, readout: 'repo bez uzupełnionych topików' },
  ] satisfies ExampleRow[],
  health: [
    { label: 'Merge rate', points: 24, maxPoints: 25, readout: '96% PR-ów zmergowanych' },
    { label: 'Czas rozstrzygnięcia PR', points: 18, maxPoints: 25, readout: 'mediana 4 dni' },
    { label: 'Czas zamykania issues', points: null, maxPoints: 15, readout: 'za mało zamkniętych issues' },
  ] satisfies ExampleRow[],
}

// Wagi zgodne ze SPEC §6. Suma każdej listy to 100.
export const matchWeights = [
  { label: 'Dopasowanie języka', weight: 30 },
  { label: 'Dopasowanie tematyki', weight: 25 },
  { label: 'Przystępność kodu', weight: 25 },
  { label: 'Wielkość społeczności', weight: 20 },
]

export const healthWeights = [
  { label: 'Merge rate', weight: 25 },
  { label: 'Czas rozstrzygnięcia PR', weight: 25 },
  { label: 'Odsetek zastałych PR-ów', weight: 20 },
  { label: 'Aktywność', weight: 15 },
  { label: 'Czas zamykania issues', weight: 15 },
]
