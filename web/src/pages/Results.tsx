import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { RepoCard } from '../components/RepoCard'
import { ScoreBreakdown } from '../components/ScoreBreakdown'
import { EmptyState } from '../components/states/EmptyState'
import { ErrorState, InlineError } from '../components/states/ErrorState'
import { ResultsSkeleton } from '../components/states/Skeleton'
import { StaleBanner } from '../components/states/StaleBanner'
import { useRecommendations } from '../hooks/useRecommendations'
import { useAddToWatchlist, useWatchedNames } from '../hooks/useWatchlist'
import type { RecommendationQuery } from '../api/types'
import { parseMaxDifficulty } from '../lib/difficulty'
import { pickHighlights, scoreBands } from '../lib/highlight'
import { starBand } from '../lib/starBand'
import { buttonPrimary } from '../lib/ui'

export function Results() {
  const [params] = useSearchParams()
  const [explaining, setExplaining] = useState<string | null>(null)

  const query = useMemo<RecommendationQuery | null>(() => {
    const login = params.get('login')?.trim()
    if (!login) return null

    return {
      login,
      languages: (params.get('languages') ?? '')
        .split(',')
        .map((l) => l.trim())
        .filter(Boolean),
      targetStars: Number(params.get('targetStars') ?? 500) || 500,
      maxDifficulty: parseMaxDifficulty(params.get('maxDifficulty')),
    }
  }, [params])

  const recommendations = useRecommendations(query)
  const watched = useWatchedNames()
  const add = useAddToWatchlist()

  const items = recommendations.data?.data.items ?? []
  const highlights = useMemo(() => pickHighlights(items), [items])
  const bands = useMemo(() => scoreBands(items), [items])
  const band = query ? starBand(query.targetStars) : null
  const explained = items.find((item) => item.fullName === explaining) ?? null

  if (query === null) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-16 sm:px-6">
        <EmptyState
          title="Brak loginu w adresie"
          hints={['Wróć na stronę główną i podaj login GitHub.']}
          action={
            <Link to="/" className={buttonPrimary}>
              Na stronę główną
            </Link>
          }
        />
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <header className="border-b border-rule pb-6">
        <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-2">
          <div>
            <p className="label text-muted">Rekomendacje dla</p>
            <h1 className="display mt-2 font-mono text-2xl text-ink">{query.login}</h1>
          </div>

          <Link to={`/?${params}`} className="text-sm text-muted underline decoration-rule-strong underline-offset-4 transition hover:text-ink">
            Zmień kryteria
          </Link>
        </div>

        <p className="num mt-4 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-muted">
          <span className="text-ink-soft">
            {query.languages.length > 0 ? query.languages.join(', ') : 'języki z profilu'}
          </span>
          {band && (
            <>
              <span aria-hidden="true">·</span>
              <span>
                {band.lo.toLocaleString('pl-PL')}-{band.hi.toLocaleString('pl-PL')} ★
              </span>
            </>
          )}
          {query.maxDifficulty !== null && (
            <>
              <span aria-hidden="true">·</span>
              <span>trudność do {query.maxDifficulty}</span>
            </>
          )}
        </p>
      </header>

      {recommendations.data?.isStale && (
        <div className="mt-6">
          <StaleBanner computedAt={items[0]?.dataFreshness.repo ?? null} />
        </div>
      )}

      {recommendations.isPending && (
        <div className="mt-8">
          <p className="mb-6 text-sm text-muted">
            Pierwszy przebieg to około stu wywołań do GitHuba, więc potrwa kilka sekund.
          </p>
          <ResultsSkeleton />
        </div>
      )}

      {recommendations.isError && (
        <div className="mt-8">
          <ErrorState error={recommendations.error} onRetry={() => recommendations.refetch()} />
        </div>
      )}

      {recommendations.data && items.length === 0 && (
        <div className="mt-8">
          <EmptyState
            title="Nic nie przeszło przez filtry"
            hints={recommendations.data.data.hints}
            action={
              <Link to="/" className={buttonPrimary}>
                Popraw kryteria
              </Link>
            }
          />
        </div>
      )}

      {items.length > 0 && (
        <>
          {/* Zastępuje wycięty komponent punktowy: istnienie wolnego issue jest
              filtrem, nie punktami, więc mówimy o tym raz nad listą (SPEC §0.2). */}
          <p className="mt-6 text-sm text-muted">
            Wszystkie wyniki mają co najmniej jedno nieprzypisane issue. Zacieniowane pole na skali to rozrzut
            tej miary w całej dziesiątce.
          </p>

          {add.isError && (
            <div className="mt-5">
              <InlineError error={add.error} onDismiss={() => add.reset()} />
            </div>
          )}

          <div className="mt-6 grid gap-4 lg:grid-cols-2">
            {items.map((item) => (
              <RepoCard
                key={item.fullName}
                item={item}
                highlight={highlights.get(item.fullName)}
                bands={bands}
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
