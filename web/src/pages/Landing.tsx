import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { LanguageChips } from '../components/LanguageChips'
import { RailRow, ScoreRail } from '../components/ScoreRail'
import { ErrorState } from '../components/states/ErrorState'
import { LineSkeleton } from '../components/states/Skeleton'
import { StaleBanner } from '../components/states/StaleBanner'
import { useProfile } from '../hooks/useProfile'
import { parseMaxDifficulty } from '../lib/difficulty'
import { exampleRepo, healthWeights, matchWeights, type ExampleRow } from '../lib/example'
import { nearestStopIndex, starBand, starStops } from '../lib/starBand'
import { buttonPrimary, field } from '../lib/ui'

// Kolejność ma znaczenie, więc numeracja niesie treść, a nie ozdobę: bez
// loginu nie ma języków, bez języków nie ma czego szukać.
const steps = [
  {
    title: 'Podajesz login GitHub',
    detail: 'Czytamy publiczne repozytoria i kontrybucje. Bez logowania, bez zapisywania czegokolwiek o Tobie.',
  },
  {
    title: 'Potwierdzasz języki i skalę projektu',
    detail: 'Wykryte języki są propozycją. Suwak wielkości zmienia zapytanie do GitHuba, nie same wagi.',
  },
  {
    title: 'Dostajesz dziesięć kart z rozbiciem',
    detail: 'Przy każdej ocenie widać komponenty, wartości źródłowe i to, czego nie dało się policzyć.',
  },
]

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
  const [maxDifficulty, setMaxDifficulty] = useState<number | null>(() =>
    parseMaxDifficulty(params.get('maxDifficulty')),
  )

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
    const query = new URLSearchParams({ login: submitted, targetStars: String(targetStars) })
    if (languages.length > 0) query.set('languages', languages.join(','))
    if (maxDifficulty !== null) query.set('maxDifficulty', String(maxDifficulty))

    navigate(`/wyniki?${query}`)
  }

  return (
    <>
      <section className="mx-auto max-w-6xl px-4 pt-12 pb-16 sm:px-6 lg:pt-20">
        <div className="grid gap-12 lg:grid-cols-12 lg:gap-16">
          <div className="lg:col-span-7">
            <p className="label text-muted">Filtr jakości dla „good first issues"</p>

            <h1 className="display mt-4 text-4xl leading-[1.06] text-ink sm:text-5xl">
              Do których repozytoriów warto zacząć kontrybutować w tym tygodniu?
            </h1>

            <p className="mt-5 max-w-xl text-base leading-relaxed text-muted">
              Podaj swój login GitHub. Sprawdzimy, czego używasz, i poszukamy projektów, które faktycznie żyją,
              mają wolne good first issues, a maintainer odpowiada na PR-y.
            </p>

            <form onSubmit={analyze} className="mt-8 flex max-w-lg flex-col gap-2 sm:flex-row">
              <input
                value={login}
                onChange={(e) => setLogin(e.target.value)}
                placeholder="login GitHub, np. octocat"
                aria-label="Login GitHub"
                autoFocus
                className={field}
              />
              <button type="submit" disabled={login.trim().length === 0} className={`${buttonPrimary} shrink-0`}>
                Analizuj profil
              </button>
            </form>

            <ol className="mt-10 max-w-lg border-t border-rule">
              {steps.map((step, index) => (
                <li key={step.title} className="flex gap-4 border-b border-rule py-3.5">
                  <span className="label num pt-1 text-faint">{index + 1}</span>
                  <span>
                    <span className="block text-sm text-ink">{step.title}</span>
                    <span className="mt-0.5 block text-sm leading-snug text-muted">{step.detail}</span>
                  </span>
                </li>
              ))}
            </ol>
          </div>

          <div className="lg:col-span-5">
            {profile.isPending && submitted.length > 0 && <CriteriaSkeleton />}

            {profile.isError && <ErrorState error={profile.error} onRetry={() => profile.refetch()} />}

            {profile.data && (
              <div className="rounded-panel border border-rule bg-panel p-5">
                {profile.data.isStale && (
                  <div className="mb-5">
                    <StaleBanner computedAt={profile.data.data.computedAt} />
                  </div>
                )}

                <h2 className="label text-muted">Kryteria wyszukiwania</h2>

                <div className="mt-4 space-y-6">
                  <div>
                    <p className="text-sm text-ink">Wykryte języki</p>
                    <p className="mt-1 text-xs leading-relaxed text-muted">
                      Z {profile.data.data.publicRepoCount} publicznych repozytoriów i projektów, do których
                      kontrybutowałeś. Odznacz, dołóż, decydujesz sam.
                    </p>
                    <div className="mt-3">
                      <LanguageChips detected={detected} selected={languages} onChange={setLanguages} />
                    </div>
                  </div>

                  <div>
                    <label htmlFor="target-stars" className="text-sm text-ink">
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
                      className="mt-3 w-full accent-ink"
                    />
                    <p className="mt-2 text-xs leading-relaxed text-muted">
                      Szukam w przedziale{' '}
                      <span className="num text-ink">
                        {band.lo.toLocaleString('pl-PL')}-{band.hi.toLocaleString('pl-PL')} ★
                      </span>{' '}
                      - suwak zmienia zapytanie do GitHuba, więc dostaniesz inne repozytoria, a nie te same karty
                      z przeliczonymi punktami.
                    </p>
                  </div>

                  <div>
                    <span className="text-sm text-ink">Maksymalna trudność issues</span>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {difficultyOptions.map((option) => (
                        <button
                          key={option.label}
                          type="button"
                          onClick={() => setMaxDifficulty(option.value)}
                          aria-pressed={maxDifficulty === option.value}
                          className={`rounded-chip border px-3 py-1.5 text-sm transition ${
                            maxDifficulty === option.value
                              ? 'border-ink bg-ink text-on-ink'
                              : 'border-rule text-muted hover:border-rule-strong hover:text-ink'
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
                    className={`${buttonPrimary} w-full`}
                  >
                    Szukaj rekomendacji
                  </button>
                </div>
              </div>
            )}

            {!profile.data && !profile.isError && !(profile.isPending && submitted.length > 0) && <WorkedExample />}
          </div>
        </div>
      </section>

      <Method />
      <Limits />
    </>
  )
}

// Zamiast obiecywać, pokazujemy jedną kartę tak, jak wygląda naprawdę -
// razem z komponentem, dla którego zabrakło danych.
function WorkedExample() {
  return (
    <figure className="rounded-panel border border-rule bg-panel p-5">
      <figcaption className="flex items-baseline justify-between gap-3">
        <span className="label text-muted">Przykład odczytu</span>
        <span className="font-mono text-xs text-muted">{exampleRepo.fullName}</span>
      </figcaption>

      <div className="mt-5 space-y-6">
        <ExampleAxis
          title="Match Score"
          axis="match"
          score={exampleRepo.matchScore}
          rows={exampleRepo.match}
        />
        <ExampleAxis
          title="Health Score"
          axis="health"
          score={exampleRepo.healthScore}
          rows={exampleRepo.health}
        />
      </div>

      <p className="mt-6 border-t border-rule pt-4 text-xs leading-relaxed text-muted">
        Wartości są przykładowe, nie odczytem na żywo. Przy prawdziwych wynikach to samo rozbicie otwiera się
        pod „Skąd te liczby?" na każdej karcie.
      </p>
    </figure>
  )
}

function ExampleAxis({
  title,
  axis,
  score,
  rows,
}: {
  title: string
  axis: 'match' | 'health'
  score: number
  rows: ExampleRow[]
}) {
  return (
    <div>
      <div className="flex items-baseline justify-between gap-3">
        <h3 className="display text-sm text-ink">{title}</h3>
        <span className={`display num text-xl ${axis === 'match' ? 'text-copper-ink' : 'text-patina-ink'}`}>
          {score.toFixed(1)}
        </span>
      </div>

      <div className="mt-2">
        <ScoreRail value={score} axis={axis} label={title} />
      </div>

      <ul className="mt-4 space-y-3">
        {rows.map((row) => (
          <li key={row.label}>
            <RailRow
              label={row.label}
              axis={axis}
              size="sm"
              value={row.points}
              max={row.maxPoints}
              readout={row.points === null ? 'za mało danych' : `${row.points.toFixed(1)} / ${row.maxPoints}`}
              note={row.readout}
            />
          </li>
        ))}
      </ul>
    </div>
  )
}

function CriteriaSkeleton() {
  return (
    <div className="rounded-panel border border-rule bg-panel p-5">
      <LineSkeleton className="h-2.5 w-32" />
      <div className="mt-5 flex flex-wrap gap-2">
        <LineSkeleton className="h-8 w-32" />
        <LineSkeleton className="h-8 w-40" />
        <LineSkeleton className="h-8 w-28" />
      </div>
      <LineSkeleton className="mt-6 h-1.5 w-full" />
      <LineSkeleton className="mt-6 h-10 w-full" />
    </div>
  )
}

function Method() {
  return (
    <section className="border-t border-rule">
      <div className="mx-auto max-w-6xl px-4 py-16 sm:px-6">
        <div className="grid gap-10 md:grid-cols-12 md:gap-14">
          <div className="md:col-span-4">
            <p className="label text-muted">Metodyka</p>
            <h2 className="display mt-3 text-2xl text-ink sm:text-3xl">Skąd biorą się liczby</h2>
            <p className="mt-4 text-sm leading-relaxed text-muted">
              Dwa niezależne wyniki, każdy to suma ważona. Komponent bez danych zwraca „za mało danych", a nie
              zero - wynik procentuje się po tym, co wiemy.
            </p>
          </div>

          <div className="grid gap-10 md:col-span-8 sm:grid-cols-2">
            <WeightBlock
              title="Match Score"
              subtitle="jak bardzo repo pasuje do Ciebie"
              axis="match"
              rows={matchWeights}
            />
            <WeightBlock
              title="Health Score"
              subtitle="czy repo żyje, niezależnie od tego, kim jesteś"
              axis="health"
              rows={healthWeights}
            />
          </div>
        </div>
      </div>
    </section>
  )
}

function WeightBlock({
  title,
  subtitle,
  axis,
  rows,
}: {
  title: string
  subtitle: string
  axis: 'match' | 'health'
  rows: Array<{ label: string; weight: number }>
}) {
  const color = axis === 'match' ? 'var(--copper)' : 'var(--patina)'

  return (
    <div>
      <h3 className="display text-base text-ink">{title}</h3>
      <p className="mt-1 text-xs text-muted">{subtitle}</p>

      {/* Jeden pasek na cały wynik, segmenty to wagi - model widać jako
          całość, a nie jako listę osobnych liczb. */}
      <div className="mt-4 flex h-2 w-full overflow-hidden rounded-[1px]" aria-hidden="true">
        {rows.map((row, index) => (
          <div
            key={row.label}
            style={{
              width: `${row.weight}%`,
              background: color,
              opacity: 0.9 - index * 0.14,
              borderRight: index === rows.length - 1 ? undefined : '1px solid var(--paper)',
            }}
          />
        ))}
      </div>

      <ul className="mt-4 space-y-2">
        {rows.map((row) => (
          <li key={row.label} className="flex items-baseline gap-2 text-sm">
            <span className="text-ink-soft">{row.label}</span>
            <span className="min-w-4 flex-1 translate-y-[-0.25rem] border-b border-dashed border-rule" />
            <span className="num shrink-0 text-muted">{row.weight}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}

const limits = [
  {
    title: 'Nie kuratorujemy listy',
    detail: 'Każdy przebieg to około stu zapytań do GitHuba liczonych na Twoje kryteria, nie gotowa pula repo.',
  },
  {
    title: 'Nie ma modelu językowego w scoringu',
    detail: 'Wynik to jawny wzór. Każdy komponent pokazuje wartość źródłową obok punktów.',
  },
  {
    title: 'Trudność issue to heurystyka',
    detail: 'Liczymy ją z labeli, długości opisu i liczby komentarzy - nie z analizy kodu.',
  },
  {
    title: 'Nie ma kont',
    detail: 'Watchlista siedzi pod anonimowym UUID w localStorage. Wyczyszczenie danych strony kasuje listę.',
  },
]

function Limits() {
  return (
    <section className="border-t border-rule">
      <div className="mx-auto max-w-6xl px-4 py-16 sm:px-6">
        <div className="grid gap-10 md:grid-cols-12 md:gap-14">
          <div className="md:col-span-4">
            <p className="label text-muted">Granice</p>
            <h2 className="display mt-3 text-2xl text-ink sm:text-3xl">Czego Gittez nie robi</h2>
          </div>

          <ul className="grid gap-px overflow-hidden rounded-panel border border-rule bg-rule md:col-span-8 sm:grid-cols-2">
            {limits.map((limit) => (
              <li key={limit.title} className="bg-panel p-5">
                <h3 className="text-sm font-medium text-ink">{limit.title}</h3>
                <p className="mt-1.5 text-sm leading-relaxed text-muted">{limit.detail}</p>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  )
}
