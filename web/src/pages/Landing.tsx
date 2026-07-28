import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { LanguageChips } from '../components/LanguageChips'
import { ErrorState } from '../components/states/ErrorState'
import { LineSkeleton } from '../components/states/Skeleton'
import { StaleBanner } from '../components/states/StaleBanner'
import { useProfile } from '../hooks/useProfile'
import { nearestStopIndex, starBand, starStops } from '../lib/starBand'

const difficultyOptions = [
  { value: 1, label: 'tylko łatwe' },
  { value: 2, label: 'do średnich' },
  { value: null, label: 'bez ograniczeń' },
] as const

export function Landing() {
  const navigate = useNavigate()

  // Powrót z wyników („Zmień kryteria") wraca z kompletem parametrów, więc
  // login i zaznaczone chipy nie znikają przy każdej poprawce jednego suwaka.
  const [params] = useSearchParams()
  const restored = useRef(params.get('languages') !== null)

  const [login, setLogin] = useState(params.get('login') ?? '')
  const [submitted, setSubmitted] = useState(params.get('login') ?? '')
  const [languages, setLanguages] = useState<string[]>(() =>
    (params.get('languages') ?? '')
      .split(',')
      .map((l) => l.trim())
      .filter(Boolean),
  )
  const [stopIndex, setStopIndex] = useState(nearestStopIndex(Number(params.get('targetStars')) || 500))
  const [maxDifficulty, setMaxDifficulty] = useState<number | null>(() => {
    const raw = params.get('maxDifficulty')
    return raw === null || Number.isNaN(Number(raw)) ? null : Number(raw)
  })

  const profile = useProfile(submitted)
  const detected = profile.data?.data.languages ?? []

  useEffect(() => {
    if (!profile.data) return

    // Wybór odtworzony z adresu wygrywa z automatycznym zaznaczeniem, ale tylko
    // raz: analiza kolejnego loginu ma znów zaproponować jego trzy języki.
    if (restored.current) {
      restored.current = false
      return
    }

    setLanguages(profile.data.data.languages.slice(0, 3).map((l) => l.name))
  }, [profile.data])

  const targetStars = starStops[stopIndex]
  const band = starBand(targetStars)

  const analyze = (e: FormEvent) => {
    e.preventDefault()
    const value = login.trim()
    if (value.length > 0) setSubmitted(value)
  }

  const search = () => {
    const params = new URLSearchParams({ login: submitted, targetStars: String(targetStars) })
    if (languages.length > 0) params.set('languages', languages.join(','))
    if (maxDifficulty !== null) params.set('maxDifficulty', String(maxDifficulty))

    navigate(`/wyniki?${params}`)
  }

  return (
    <div className="mx-auto max-w-2xl px-4 py-16">
      <h1 className="text-3xl font-semibold text-white sm:text-4xl">
        Do których repozytoriów warto zacząć kontrybutować w tym tygodniu?
      </h1>
      <p className="mt-4 text-ink-400">
        Podaj swój login GitHub. Sprawdzimy, czego używasz, i poszukamy projektów, które faktycznie żyją,
        mają wolne good first issues, a maintainer odpowiada na PR-y.
      </p>

      <form onSubmit={analyze} className="mt-8 flex gap-2">
        <input
          value={login}
          onChange={(e) => setLogin(e.target.value)}
          placeholder="login GitHub, np. octocat"
          autoFocus
          className="flex-1 rounded-lg border border-ink-700 bg-ink-900 px-4 py-2.5 text-ink-200 outline-none placeholder:text-ink-700 focus:border-sky-400/60"
        />
        <button
          type="submit"
          disabled={login.trim().length === 0}
          className="rounded-lg bg-sky-500 px-5 py-2.5 font-medium text-ink-950 transition hover:bg-sky-400 disabled:cursor-not-allowed disabled:opacity-40"
        >
          Analizuj
        </button>
      </form>

      {profile.isPending && submitted.length > 0 && (
        <div className="mt-10 space-y-3">
          <LineSkeleton className="h-4 w-40" />
          <div className="flex gap-2">
            <LineSkeleton className="h-9 w-32 rounded-full" />
            <LineSkeleton className="h-9 w-40 rounded-full" />
            <LineSkeleton className="h-9 w-28 rounded-full" />
          </div>
        </div>
      )}

      {profile.isError && (
        <div className="mt-10">
          <ErrorState error={profile.error} onRetry={() => profile.refetch()} />
        </div>
      )}

      {profile.data && (
        <section className="mt-10 space-y-8">
          {profile.data.isStale && <StaleBanner computedAt={profile.data.data.computedAt} />}

          <div>
            <h2 className="text-sm font-medium uppercase tracking-wider text-ink-400">Wykryte języki</h2>
            <p className="mt-1 text-sm text-ink-400">
              Z {profile.data.data.publicRepoCount} publicznych repozytoriów i projektów, do których
              kontrybutowałeś. Odznacz, dołóż, decydujesz sam.
            </p>
            <div className="mt-4">
              <LanguageChips detected={detected} selected={languages} onChange={setLanguages} />
            </div>
          </div>

          <div>
            <label htmlFor="target-stars" className="text-sm font-medium uppercase tracking-wider text-ink-400">
              Preferowana wielkość projektu
            </label>
            <input
              id="target-stars"
              type="range"
              min={0}
              max={starStops.length - 1}
              step={1}
              value={stopIndex}
              onChange={(e) => setStopIndex(Number(e.target.value))}
              className="mt-3 w-full accent-sky-400"
            />
            <p className="mt-2 text-sm text-ink-400">
              Szukam w przedziale{' '}
              <span className="text-ink-200 tabular-nums">
                {band.lo.toLocaleString('pl-PL')}-{band.hi.toLocaleString('pl-PL')} ★
              </span>{' '}
              - suwak zmienia zapytanie do GitHuba, więc dostaniesz inne repozytoria, a nie te same karty z
              przeliczonymi punktami.
            </p>
          </div>

          <div>
            <span className="text-sm font-medium uppercase tracking-wider text-ink-400">
              Maksymalna trudność issues
            </span>
            <div className="mt-3 flex gap-2">
              {difficultyOptions.map((option) => (
                <button
                  key={option.label}
                  type="button"
                  onClick={() => setMaxDifficulty(option.value)}
                  aria-pressed={maxDifficulty === option.value}
                  className={`rounded-lg border px-3 py-1.5 text-sm transition ${
                    maxDifficulty === option.value
                      ? 'border-sky-400/60 bg-sky-500/15 text-sky-200'
                      : 'border-ink-700 text-ink-400 hover:border-ink-400 hover:text-ink-200'
                  }`}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </div>

          <button
            type="button"
            onClick={search}
            disabled={languages.length === 0}
            className="w-full rounded-lg bg-sky-500 px-5 py-3 font-medium text-ink-950 transition hover:bg-sky-400 disabled:cursor-not-allowed disabled:opacity-40"
          >
            Szukaj rekomendacji
          </button>
        </section>
      )}
    </div>
  )
}
