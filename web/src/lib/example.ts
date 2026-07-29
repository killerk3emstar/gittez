// Przykłady na stronie głównej: pokazujemy produkt tym, co go odróżnia, czyli
// rozbiciem oceny. Wartości są ilustracją, nie odczytem - strona mówi to
// wprost obok, a nie w przypisie.
//
// Każdy przykład ma po trzy wiersze na oś, żeby przełączenie nie skakało
// wysokością, a dwa z trzech niosą komponent bez danych - bo to właśnie ta
// szczerość jest tu do pokazania.
export type ExampleRow = {
  label: string
  points: number | null
  maxPoints: number
  readout: string
}

export type Example = {
  fullName: string
  matchScore: number
  healthScore: number
  match: ExampleRow[]
  health: ExampleRow[]
}

export const examples: Example[] = [
  {
    fullName: 'MudBlazor/MudBlazor',
    matchScore: 78.5,
    healthScore: 84,
    match: [
      { label: 'Dopasowanie języka', points: 30, maxPoints: 30, readout: 'C# - miejsce 1 w Twoim rankingu' },
      { label: 'Przystępność kodu', points: 19.5, maxPoints: 25, readout: 'mniejsze niż 78% kandydatów' },
      { label: 'Dopasowanie tematyki', points: null, maxPoints: 25, readout: 'repo bez uzupełnionych topików' },
    ],
    health: [
      { label: 'Merge rate', points: 24, maxPoints: 25, readout: '96% PR-ów zmergowanych' },
      { label: 'Czas rozstrzygnięcia PR', points: 18, maxPoints: 25, readout: 'mediana 4 dni' },
      { label: 'Czas zamykania issues', points: null, maxPoints: 15, readout: 'za mało zamkniętych issues' },
    ],
  },
  {
    fullName: 'quartznet/quartznet',
    matchScore: 71.2,
    healthScore: 76,
    match: [
      { label: 'Dopasowanie języka', points: 30, maxPoints: 30, readout: 'C# - miejsce 1 w Twoim rankingu' },
      { label: 'Dopasowanie tematyki', points: 16, maxPoints: 25, readout: '2 z 5 topików wspólne' },
      { label: 'Wielkość społeczności', points: 15.4, maxPoints: 20, readout: 'blisko preferowanej skali' },
    ],
    health: [
      { label: 'Merge rate', points: 21, maxPoints: 25, readout: '84% PR-ów zmergowanych' },
      { label: 'Czas rozstrzygnięcia PR', points: 20, maxPoints: 25, readout: 'mediana 3 dni' },
      { label: 'Aktywność', points: 12, maxPoints: 15, readout: 'push 5 dni temu' },
    ],
  },
  {
    fullName: 'serilog/serilog-sinks-file',
    matchScore: 64.8,
    healthScore: 69,
    match: [
      { label: 'Dopasowanie języka', points: 30, maxPoints: 30, readout: 'C# - miejsce 1 w Twoim rankingu' },
      { label: 'Przystępność kodu', points: 22.5, maxPoints: 25, readout: 'mniejsze niż 91% kandydatów' },
      { label: 'Wielkość społeczności', points: 11.8, maxPoints: 20, readout: 'mniejsze niż preferowana skala' },
    ],
    health: [
      { label: 'Merge rate', points: 22, maxPoints: 25, readout: '88% PR-ów zmergowanych' },
      { label: 'Czas zamykania issues', points: 10, maxPoints: 15, readout: 'mediana 21 dni' },
      { label: 'Odsetek zastałych PR-ów', points: null, maxPoints: 20, readout: 'brak otwartych PR-ów' },
    ],
  },
]

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
