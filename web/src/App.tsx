import { useQuery } from '@tanstack/react-query'
import { NavLink, Route, Routes } from 'react-router-dom'
import { api } from './api/client'
import { Landing } from './pages/Landing'
import { Results } from './pages/Results'
import { Watchlist } from './pages/Watchlist'
import { useWatchlist } from './hooks/useWatchlist'

export default function App() {
  return (
    <div className="min-h-screen">
      <Header />

      <main>
        <Routes>
          <Route path="/" element={<Landing />} />
          <Route path="/wyniki" element={<Results />} />
          <Route path="/watchlista" element={<Watchlist />} />
          <Route path="*" element={<Landing />} />
        </Routes>
      </main>
    </div>
  )
}

function Header() {
  const watchlist = useWatchlist()
  const count = watchlist.data?.data.length ?? 0

  return (
    <header className="border-b border-ink-800">
      <div className="mx-auto flex max-w-5xl items-center gap-6 px-4 py-4">
        <NavLink to="/" className="font-semibold text-white">
          Gittez
        </NavLink>

        <nav className="flex items-center gap-4 text-sm">
          <NavLink
            to="/watchlista"
            className={({ isActive }) => (isActive ? 'text-sky-300' : 'text-ink-400 transition hover:text-ink-200')}
          >
            Watchlista{count > 0 && ` (${count})`}
          </NavLink>
        </nav>

        <div className="ml-auto">
          <RateLimitIndicator />
        </div>
      </div>
    </header>
  )
}

// Widoczny licznik limitu jest tu z tego samego powodu, co rozbicie wyników:
// recenzent ma widzieć, ile budżetu zjada przebieg, a nie zgadywać.
function RateLimitIndicator() {
  const health = useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => api.health(signal),
    staleTime: 30 * 1000,
    retry: false,
  })

  const core = health.data?.data.rateLimit?.core
  if (!core) return null

  const low = core.remaining < core.limit * 0.1

  return (
    <span
      className={`text-xs tabular-nums ${low ? 'text-amber-300' : 'text-ink-400'}`}
      title={`Reset limitu: ${new Date(core.resetAt).toLocaleTimeString('pl-PL')}`}
    >
      GitHub API: {core.remaining} / {core.limit}
    </span>
  )
}
