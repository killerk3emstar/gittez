import { NavLink, Route, Routes } from 'react-router-dom'
import { RateLimitDetail, RateLimitMeter } from './components/RateLimitMeter'
import { ThemeToggle } from './components/ThemeToggle'
import { Landing } from './pages/Landing'
import { Results } from './pages/Results'
import { Watchlist } from './pages/Watchlist'
import { useWatchlist } from './hooks/useWatchlist'

export default function App() {
  return (
    <div className="flex min-h-screen flex-col">
      <a
        href="#tresc"
        className="sr-only focus:not-sr-only focus:absolute focus:top-3 focus:left-3 focus:z-50 focus:rounded-chip focus:bg-ink focus:px-3 focus:py-2 focus:text-sm focus:text-on-ink"
      >
        Przejdź do treści
      </a>

      <Header />

      <main id="tresc" className="flex-1">
        <Routes>
          <Route path="/" element={<Landing />} />
          <Route path="/wyniki" element={<Results />} />
          <Route path="/watchlista" element={<Watchlist />} />
          <Route path="*" element={<Landing />} />
        </Routes>
      </main>

      <Footer />
    </div>
  )
}

function Header() {
  const watchlist = useWatchlist()
  const count = watchlist.data?.data.length ?? 0

  return (
    <header className="sticky top-0 z-40 border-b border-rule bg-paper/92 backdrop-blur">
      <div className="mx-auto flex h-14 max-w-6xl items-center gap-3 px-4 sm:gap-4 sm:px-6">
        <NavLink to="/" className="wordmark text-ink" aria-label="Gittez, strona główna">
          GITTEZ
        </NavLink>

        <NavLink
          to="/watchlista"
          className={({ isActive }) =>
            `flex items-center gap-1.5 border-b-2 py-1 text-sm transition ${
              isActive ? 'border-ink text-ink' : 'border-transparent text-muted hover:text-ink'
            }`
          }
        >
          Watchlista
          {count > 0 && <span className="num rounded-chip bg-sunk px-1.5 py-0.5 text-xs text-ink-soft">{count}</span>}
        </NavLink>

        <div className="ml-auto flex items-center gap-3">
          <RateLimitMeter />
          <ThemeToggle />
        </div>
      </div>
    </header>
  )
}

function Footer() {
  return (
    <footer className="mt-24 border-t border-rule">
      <div className="mx-auto grid max-w-6xl gap-8 px-4 py-10 sm:px-6 md:grid-cols-3">
        <div>
          <p className="wordmark text-ink">GITTEZ</p>
          <p className="mt-2 max-w-xs text-xs leading-relaxed text-muted">
            Filtr jakości dla „good first issues": dopasowanie do Twojego profilu i ocena responsywności
            maintainera, liczone na żywo z API GitHuba.
          </p>
        </div>

        <div>
          <p className="label text-muted">Budżet zapytań</p>
          <div className="mt-2">
            <RateLimitDetail />
          </div>
        </div>

        <div>
          <p className="label text-muted">Zaplecze</p>
          <ul className="mt-2 space-y-1 text-xs text-muted">
            <li>
              <a
                href="https://github.com/killerk3emstar/gittez"
                target="_blank"
                rel="noreferrer"
                className="underline decoration-rule-strong underline-offset-2 transition hover:text-ink"
              >
                Kod źródłowy
              </a>
            </li>
            <li>
              <a
                href="/api/health"
                className="underline decoration-rule-strong underline-offset-2 transition hover:text-ink"
              >
                Status API
              </a>
            </li>
            <li>Watchlista żyje w localStorage tej przeglądarki, bez konta.</li>
          </ul>
        </div>
      </div>
    </footer>
  )
}
