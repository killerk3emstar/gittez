import type { RateLimitPool } from '../api/types'
import { useHealth } from '../hooks/useHealth'
import { ScoreRail, type Axis } from './ScoreRail'

function ratio(pool: RateLimitPool): number {
  return pool.limit > 0 ? pool.remaining / pool.limit : 0
}

function tone(pool: RateLimitPool): Axis {
  if (pool.remaining <= 0) return 'danger'
  return ratio(pool) < 0.1 ? 'warn' : 'neutral'
}

function textTone(pool: RateLimitPool): string {
  if (pool.remaining <= 0) return 'text-rust'
  return ratio(pool) < 0.1 ? 'text-amber' : 'text-ink'
}

function resetAt(pool: RateLimitPool): string {
  return new Date(pool.resetAt).toLocaleTimeString('pl-PL', { hour: '2-digit', minute: '2-digit' })
}

// Licznik jest w nagłówku zawsze, w każdym stanie - również zanim padnie
// pierwsze zapytanie do GitHuba i wtedy, gdy /api/health nie odpowiada.
// Znikający wskaźnik budżetu jest gorszy niż wskaźnik mówiący "nie wiem".
//
// Etykieta zawsze mówi o tej samej puli. Wcześniej nagłówek przełączał się na
// pulę bardziej ograniczającą i "GitHub API" zmieniało się w "GitHub search"
// pod ręką - mniej informacji jest lepsze niż etykieta zmieniająca znaczenie.
export function RateLimitMeter() {
  const health = useHealth()
  const limit = health.data?.data.rateLimit
  const core = limit?.core ?? null
  const search = limit?.search ?? null

  // Search ma 30 zapytań na minutę i to on zatrzymuje wyszukiwanie jako
  // pierwszy, więc dochodzi obok core dopiero, gdy realnie się kończy.
  const tightSearch = search !== null && ratio(search) < 0.5 ? search : null

  const title = health.isError
    ? 'Nie udało się odczytać /api/health - licznik limitu jest nieaktualny.'
    : core
      ? `Pula core: ${core.remaining} z ${core.limit} zapytań, reset o ${resetAt(core)}.` +
        (search ? ` Pula search: ${search.remaining} z ${search.limit}, reset o ${resetAt(search)}.` : '')
      : 'Limit czytamy z nagłówków X-RateLimit-* prawdziwych odpowiedzi, więc licznik zapełni się przy pierwszym zapytaniu do GitHuba.'

  return (
    <div className="flex items-center gap-2" title={title}>
      <span className="label hidden text-muted md:inline">GitHub API</span>

      {health.isError ? (
        <span className="text-xs font-medium text-rust">bez łączności</span>
      ) : core === null ? (
        <>
          <div className="w-10 sm:w-16">
            <ScoreRail value={null} size="sm" label="Limit GitHub API" />
          </div>
          <span className="num text-xs text-muted">{health.isPending ? '...' : 'bez odczytu'}</span>
        </>
      ) : (
        <>
          <div className="w-10 sm:w-16">
            <ScoreRail
              value={core.remaining}
              max={core.limit}
              axis={tone(core)}
              size="sm"
              label="Limit GitHub API"
            />
          </div>
          <span className={`num text-xs font-medium ${textTone(core)}`}>
            {core.remaining}
            <span className="hidden text-muted sm:inline">/{core.limit}</span>
          </span>
        </>
      )}

      {/* Na wąskim ekranie dopisek nie mieści się obok reszty nagłówka, a pełny
          stan obu pul z czasami resetu stoi w stopce i w tytule pola. */}
      {tightSearch && (
        <span className={`num hidden text-xs font-medium sm:inline ${textTone(tightSearch)}`}>
          <span className="text-muted">search </span>
          {tightSearch.remaining}
          <span className="hidden text-muted sm:inline">/{tightSearch.limit}</span>
        </span>
      )}
    </div>
  )
}

// Stopka mówi to samo pełnym zdaniem i pokazuje obie pule naraz - nagłówek
// musi się zmieścić na telefonie, a to jest miejsce na czas resetu.
export function RateLimitDetail() {
  const health = useHealth()
  const limit = health.data?.data.rateLimit

  if (health.isError) {
    return <p className="text-xs text-muted">Limit GitHub API: /api/health nie odpowiada.</p>
  }

  const pools: Array<[string, RateLimitPool | null]> = [
    ['GitHub API', limit?.core ?? null],
    ['GitHub search', limit?.search ?? null],
  ]

  return (
    <div className="space-y-1">
      {pools.map(([name, pool]) => (
        <p key={name} className="num text-xs text-muted">
          <span className="text-ink-soft">{name}:</span>{' '}
          {pool ? (
            <>
              {pool.remaining} z {pool.limit} zapytań, reset o {resetAt(pool)}
            </>
          ) : (
            'bez odczytu do pierwszego zapytania'
          )}
        </p>
      ))}
    </div>
  )
}
