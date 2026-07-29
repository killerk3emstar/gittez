import type { Health, RateLimitPool } from '../api/types'
import { useHealth } from '../hooks/useHealth'
import { ScoreRail, type Axis } from './ScoreRail'

const poolName: Record<'core' | 'search', string> = {
  core: 'GitHub API',
  search: 'GitHub search',
}

type Binding = {
  name: 'core' | 'search'
  pool: RateLimitPool
}

function ratio(pool: RateLimitPool): number {
  return pool.limit > 0 ? pool.remaining / pool.limit : 0
}

// W nagłówku pokazujemy pulę, która realnie ogranicza kolejne wyszukiwanie.
// Core ma 5000/h, search 30/min - to zwykle search kończy się pierwszy, a
// licznik pokazujący wyłącznie core mówiłby wtedy, że wszystko gra.
function binding(health: Health | undefined): Binding | null {
  const core = health?.rateLimit?.core ?? null
  const search = health?.rateLimit?.search ?? null

  if (core && search) return ratio(search) < ratio(core) ? { name: 'search', pool: search } : { name: 'core', pool: core }
  if (core) return { name: 'core', pool: core }
  if (search) return { name: 'search', pool: search }

  return null
}

function tone(pool: RateLimitPool): Axis {
  if (pool.remaining <= 0) return 'danger'
  return ratio(pool) < 0.1 ? 'warn' : 'neutral'
}

function resetAt(pool: RateLimitPool): string {
  return new Date(pool.resetAt).toLocaleTimeString('pl-PL', { hour: '2-digit', minute: '2-digit' })
}

// Licznik jest w nagłówku zawsze, w każdym stanie - również zanim padnie
// pierwsze zapytanie do GitHuba i wtedy, gdy /api/health nie odpowiada.
// Znikający wskaźnik budżetu jest gorszy niż wskaźnik mówiący "nie wiem".
export function RateLimitMeter() {
  const health = useHealth()
  const current = binding(health.data?.data)

  if (health.isError) {
    return (
      <Shell label="GitHub API" title="Nie udało się odczytać /api/health - licznik limitu jest nieaktualny.">
        <span className="text-xs font-medium text-rust">bez łączności</span>
      </Shell>
    )
  }

  if (!current) {
    return (
      <Shell
        label="GitHub API"
        title={
          health.isPending
            ? 'Odczytuję stan limitu z /api/health.'
            : 'Limit czytamy z nagłówków X-RateLimit-* prawdziwych odpowiedzi, więc licznik zapełni się przy pierwszym zapytaniu do GitHuba.'
        }
      >
        <div className="w-12 sm:w-16">
          <ScoreRail value={null} size="sm" label="Limit GitHub API" />
        </div>
        <span className="num text-xs text-muted">{health.isPending ? '...' : 'bez odczytu'}</span>
      </Shell>
    )
  }

  const { name, pool } = current
  const axis = tone(pool)
  const textTone = axis === 'danger' ? 'text-rust' : axis === 'warn' ? 'text-amber' : 'text-ink'

  return (
    <Shell
      label={poolName[name]}
      title={`${poolName[name]}: ${pool.remaining} z ${pool.limit} zapytań, zużyte ${pool.used}. Reset o ${resetAt(pool)}.`}
    >
      <div className="w-12 sm:w-16">
        <ScoreRail value={pool.remaining} max={pool.limit} axis={axis} size="sm" label={poolName[name]} />
      </div>
      <span className={`num text-xs font-medium ${textTone}`}>
        {pool.remaining}
        <span className="hidden text-muted sm:inline">/{pool.limit}</span>
      </span>
    </Shell>
  )
}

function Shell({ label, title, children }: { label: string; title: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-2" title={title}>
      <span className="label hidden text-muted md:inline">{label}</span>
      {children}
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

  const pools: Array<['core' | 'search', RateLimitPool | null]> = [
    ['core', limit?.core ?? null],
    ['search', limit?.search ?? null],
  ]

  return (
    <div className="space-y-1">
      {pools.map(([name, pool]) => (
        <p key={name} className="num text-xs text-muted">
          <span className="text-ink-soft">{poolName[name]}:</span>{' '}
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
