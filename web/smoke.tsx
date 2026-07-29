import { renderToString } from 'react-dom/server'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import App from './src/App'
import { ScoreBreakdown } from './src/components/ScoreBreakdown'
import type { Recommendation, RecommendationQuery, WatchlistItem } from './src/api/types'

const item: Recommendation = {
  fullName: 'MudBlazor/MudBlazor',
  description: 'Blazor Component Library based on Material Design.',
  htmlUrl: 'https://github.com/MudBlazor/MudBlazor',
  stars: 8900,
  primaryLanguage: 'C#',
  topics: ['blazor', 'material-design', 'csharp'],
  lastPushedAt: new Date(Date.now() - 3 * 86400000).toISOString(),
  matchScore: 78.5,
  healthScore: 84,
  finalScore: 80.4,
  matchBreakdown: [
    { key: 'language', label: 'Dopasowanie języka', points: 30, maxPoints: 30, rawValue: 'C# - miejsce 1', explanation: 'Twój najczęstszy język.', isSampled: false },
    { key: 'topic', label: 'Dopasowanie tematyki', points: null, maxPoints: 25, rawValue: 'brak topików', explanation: 'Repo nie ma uzupełnionych topików.', isSampled: false },
    { key: 'complexity', label: 'Przystępność kodu', points: 19.5, maxPoints: 25, rawValue: 'mniejsze niż 78% kandydatów', explanation: 'Mniejsze niż 78% kandydatów.', isSampled: false },
  ],
  healthBreakdown: [
    { key: 'merge', label: 'Merge rate', points: 24, maxPoints: 25, rawValue: '96% PR-ów zmergowanych', explanation: '96% PR-ów zmergowanych.', isSampled: false },
    { key: 'stale', label: 'Odsetek zastałych PR-ów', points: 12, maxPoints: 20, rawValue: '18% starszych niż 90 dni', explanation: 'Liczone na 100 najstarszych otwartych PR-ach.', isSampled: true },
    { key: 'issues', label: 'Czas zamykania issues', points: null, maxPoints: 15, rawValue: 'za mało zamkniętych', explanation: 'Za mało danych.', isSampled: false },
  ],
  issues: [
    { number: 1234, title: 'Fix typo in DataGrid docs', htmlUrl: 'https://github.com/x/1234', labels: ['good first issue', 'docs'], commentCount: 1, difficulty: 1, updatedAt: new Date().toISOString() },
    { number: 1235, title: 'Add virtualization option to MudList', htmlUrl: 'https://github.com/x/1235', labels: ['good first issue'], commentCount: 12, difficulty: 3, updatedAt: new Date().toISOString() },
  ],
  dataFreshness: { repo: new Date().toISOString(), health: new Date().toISOString() },
}

const second: Recommendation = {
  ...item,
  fullName: 'dead/repo',
  healthScore: 34,
  matchScore: 61.2,
  finalScore: 47.6,
  healthBreakdown: [
    { key: 'merge', label: 'Merge rate', points: 4, maxPoints: 25, rawValue: '16% PR-ów zmergowanych', explanation: 'Większość PR-ów zamykana bez merge.', isSampled: false },
  ],
}

const query: RecommendationQuery = {
  login: 'octocat',
  languages: ['C#', 'TypeScript'],
  targetStars: 500,
  maxDifficulty: null,
}

const watchlist: WatchlistItem[] = [
  {
    id: 1,
    repoFullName: 'MudBlazor/MudBlazor',
    note: 'issue #1234 na wieczór',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    repo: { description: 'Blazor Component Library', htmlUrl: 'https://github.com/MudBlazor/MudBlazor', stars: 8900, primaryLanguage: 'C#', lastPushedAt: new Date(Date.now() - 400 * 86400000).toISOString(), healthScore: 84 },
  },
  {
    id: 2,
    repoFullName: 'nieznane/repo',
    note: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    repo: null,
  },
]

const profile = {
  login: 'octocat',
  publicRepoCount: 14,
  medianSizeKb: 2400,
  languages: [
    { name: 'C#', ownedRepos: 7, contributedRepos: 2, rank: 1 },
    { name: 'TypeScript', ownedRepos: 3, contributedRepos: 1, rank: 2 },
  ],
  interests: ['blazor'],
  computedAt: new Date().toISOString(),
}

function client() {
  const c = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  c.setQueryData(['recommendations', query], { data: { items: [item, second], hints: [] }, isStale: false })
  c.setQueryData(['watchlist'], { data: watchlist, isStale: false })
  c.setQueryData(['profile', 'octocat'], { data: profile, isStale: false })
  return c
}

function render(path: string): string {
  return renderToString(
    <QueryClientProvider client={client()}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const checks: Array<[string, boolean]> = []

// renderToString wstawia <!-- --> między sąsiednie węzły tekstowe i escapuje
// apostrofy, a toLocaleString('pl-PL') rozdziela tysiące twardą spacją - to
// szum renderowania, nie treść strony.
const normalize = (html: string) =>
  html
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/&#x27;/g, "'")
    .replace(/&quot;/g, '"')
    .replace(/&amp;/g, '&')
    .replace(/\u00a0/g, ' ')

const has = (html: string, text: string) => normalize(html).includes(text)

const landing = render('/')
checks.push(['landing: nagłówek', has(landing, 'Do których repozytoriów')])
checks.push(['landing: pole loginu', has(landing, 'login GitHub, np. octocat')])
checks.push(['landing: licznik watchlisty', has(landing, 'Watchlista') && has(landing, '>2</span>')])
checks.push(['landing: przykład rozbicia', has(landing, 'Przykład odczytu') && has(landing, 'MudBlazor/MudBlazor')])
checks.push(['landing: przykład opisany jako przykład', has(landing, 'Wartości są przykładowe')])
checks.push(['landing: tabela wag', has(landing, 'Skąd biorą się liczby') && has(landing, 'Wielkość społeczności')])
checks.push(['landing: granice', has(landing, 'Czego Gittez nie robi')])

// Licznik limitu ma być widoczny w każdym stanie, także zanim /api/health
// zdąży odpowiedzieć - przed poprawką komponent zwracał wtedy null.
checks.push(['nagłówek: licznik limitu bez danych', has(landing, 'GitHub API')])

const results = render('/wyniki?login=octocat&languages=C%23%2CTypeScript&targetStars=500')
checks.push(['wyniki: nazwa repo', has(results, 'MudBlazor/MudBlazor')])
checks.push(['wyniki: zdanie o filtrze', has(results, 'co najmniej jedno nieprzypisane issue')])
checks.push(['wyniki: pasmo gwiazdek', has(results, '100-2500 ★') || has(results, '100-2 500 ★')])
checks.push(['wyniki: odczyt Match', has(results, '>79<') || has(results, '>78<')])
checks.push(['wyniki: skala Match opisana dla czytnika', has(results, 'Match: 78.5 na 100')])
checks.push(['wyniki: zdrowe repo', has(results, 'Health: 84.0 na 100')])
checks.push(['wyniki: chore repo z ostrzeżeniem', has(results, 'Health: 34.0 na 100')])
checks.push(['wyniki: chip issue', has(results, 'Fix typo in DataGrid docs')])
checks.push(['wyniki: trudność', has(results, 'łatwe') && has(results, 'trudniejsze')])
checks.push(['wyniki: brak finalScore na karcie', !has(results, '80.4') && !has(results, '80,4')])
checks.push(['wyniki: zdanie wyróżniające', has(results, 'PR-ów zmergowanych') || has(results, 'Mniejsze niż 78%')])
checks.push(['wyniki: stan watchlisty na karcie', has(results, 'Jest na watchliście')])

const restored = render('/?login=octocat&languages=Rust&targetStars=5000')
checks.push(['landing: login odtworzony z adresu', has(restored, 'value="octocat"')])
checks.push(['landing: język spoza profilu odtworzony', has(restored, 'Rust') && has(restored, 'dodany ręcznie')])
checks.push(['landing: pasmo z odtworzonego suwaka', has(restored, '1000-25000 ★') || has(restored, '1000-25 000 ★')])
checks.push(['landing: chipy z profilu widoczne', has(restored, '7 repo, 2 kontrybucje')])

// Kryteria wchodzą pod formularz, a nie w miejsce przykładu - formularz i
// ilustracja to dwie różne kategorie i nie dzielą jednego slotu.
checks.push([
  'landing: kryteria nie wypierają przykładu',
  has(restored, 'Kryteria wyszukiwania') && has(restored, 'Przykład odczytu'),
])

const watch = render('/watchlista')
checks.push(['watchlista: odmiana lat', has(watch, 'rok temu') && !has(watch, '1 lata temu')])
checks.push(['watchlista: notatka', has(watch, 'issue #1234 na wieczór')])
checks.push(['watchlista: pozycja bez cache', has(watch, "Metadanych nie ma w cache'u")])
checks.push(['watchlista: dodaj notatkę', has(watch, 'Dodaj notatkę')])

const modal = renderToString(<ScoreBreakdown item={item} onClose={() => {}} />)
checks.push(['modal: Match', has(modal, 'Match Score') && has(modal, '78.5')])
checks.push(['modal: Health', has(modal, 'Health Score') && has(modal, '84.0')])
checks.push(['modal: brak danych na szaro', has(modal, 'za mało danych')])
checks.push(['modal: dopisek o próbce', has(modal, '(próbka)')])
checks.push(['modal: wyjaśnienia', has(modal, '96% PR-ów zmergowanych')])

let failed = 0
for (const [name, ok] of checks) {
  if (!ok) failed++
  console.log(`${ok ? 'OK  ' : 'FAIL'} ${name}`)
}

console.log(`\n${checks.length - failed}/${checks.length} przeszło`)
if (failed > 0) process.exitCode = 1
