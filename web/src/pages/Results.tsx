import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { RepoCard } from '../components/RepoCard'
import { ScoreBreakdown } from '../components/ScoreBreakdown'
import { EmptyState } from '../components/states/EmptyState'
import { ErrorState } from '../components/states/ErrorState'
import { ResultsSkeleton } from '../components/states/Skeleton'
import { StaleBanner } from '../components/states/StaleBanner'
import { useRecommendations } from '../hooks/useRecommendations'
import { useAddToWatchlist, useWatchedNames } from '../hooks/useWatchlist'
import type { RecommendationQuery } from '../api/types'
import { pickHighlights } from '../lib/highlight'
import { starBand } from '../lib/starBand'

export function Results() {
  const [params] = useSearchParams()
  const [explaining, setExplaining] = useState<string | null>(null)

  const query = useMemo<RecommendationQuery | null>(() => {
    const login = params.get('login')?.trim()
    if (!login) return null

    const maxDifficulty = params.get('maxDifficulty')

    return {
      login,
      languages: (params.get('languages') ?? '')
        .split(',')
        .map((l) => l.trim())
        .filter(Boolean),
      targetStars: Number(params.get('targetStars') ?? 500) || 500,
      maxDifficulty: maxDifficulty === null ? null : Number(maxDifficulty),
    }
  }, [params])

  const recommendations = useRecommendations(query)
  const watched = useWatchedNames()
  const add = useAddToWatchlist()

  const items = recommendations.data?.data.items ?? []
  const highlights = useMemo(() => pickHighlights(items), [items])
  const band = query ? starBand(query.targetStars) : null
  const explained = items.find((item) => item.fullName === explaining) ?? null

  if (query === null) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-16">
        <EmptyState
          title="Brak loginu w adresie"
          hints={['Wróć na stronę główną i podaj login GitHub.']}
          action={
            <Link to="/" className="rounded-lg bg-sky-500 px-4 py-2 font-medium text-ink-950 hover:bg-sky-400">
              Na stronę główną
            </Link>
          }
        />
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-5xl px-4 py-10">
      <header className="mb-8">
        <div className="flex flex-wrap items-baseline justify-between gap-3">
          <h1 className="text-2xl font-semibold text-white">Rekomendacje dla {query.login}</h1>
          <Link to="/" className="text-sm text-ink-400 transition hover:text-ink-200">
            Zmień kryteria
          </Link>
        </div>

        <p className="mt-2 text-sm text-ink-400">
          {query.languages.length > 0 ? query.languages.join(', ') : 'języki z profilu'}
          {band && ` · ${band.lo.toLocaleString('pl-PL')}-${band.hi.toLocaleString('pl-PL')} ★`}
          {query.maxDifficulty !== null && ` · trudność do ${query.maxDifficulty}`}
        </p>
      </header>

      {recommendations.data?.isStale && (
        <div className="mb-6">
          <StaleBanner computedAt={items[0]?.dataFreshness.repo ?? null} />
        </div>
      )}

      {recommendations.isPending && (
        <>
          <p className="mb-6 text-sm text-ink-400">
            Pierwszy przebieg to około stu wywołań do GitHuba, więc potrwa kilka sekund.
          </p>
          <ResultsSkeleton />
        </>
      )}

      {recommendations.isError && (
        <ErrorState error={recommendations.error} onRetry={() => recommendations.refetch()} />
      )}

      {recommendations.data && items.length === 0 && (
        <EmptyState
          title="Nic nie przeszło przez filtry"
          hints={recommendations.data.data.hints}
          action={
            <Link to="/" className="rounded-lg bg-sky-500 px-4 py-2 font-medium text-ink-950 hover:bg-sky-400">
              Popraw kryteria
            </Link>
          }
        />
      )}

      {items.length > 0 && (
        <>
          {/* Zastępuje wycięty komponent punktowy: istnienie wolnego issue jest
              filtrem, nie punktami, więc mówimy o tym raz nad listą (SPEC §0.2). */}
          <p className="mb-5 rounded-xl border border-ink-800 bg-ink-900/40 px-4 py-3 text-sm text-ink-400">
            Wszystkie wyniki mają co najmniej jedno nieprzypisane issue.
          </p>

          <div className="grid gap-4 lg:grid-cols-2">
            {items.map((item) => (
              <RepoCard
                key={item.fullName}
                item={item}
                highlight={highlights.get(item.fullName)}
                isWatched={watched.has(item.fullName.toLowerCase())}
                isSaving={add.isPending && add.variables?.repoFullName === item.fullName}
                onToggleWatch={() => add.mutate({ repoFullName: item.fullName })}
                onExplain={() => setExplaining(item.fullName)}
              />
            ))}
          </div>
        </>
      )}

      {explained && <ScoreBreakdown item={explained} onClose={() => setExplaining(null)} />}
    </div>
  )
}
