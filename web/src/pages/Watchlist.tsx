import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { WatchlistItem } from '../api/types'
import { EmptyState } from '../components/states/EmptyState'
import { ErrorState } from '../components/states/ErrorState'
import { LineSkeleton } from '../components/states/Skeleton'
import { useRemoveFromWatchlist, useUpdateNote, useWatchlist } from '../hooks/useWatchlist'
import { formatAgo, formatStars } from '../lib/format'

const maxNoteLength = 500

export function Watchlist() {
  const watchlist = useWatchlist()
  const items = watchlist.data?.data ?? []

  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <header className="mb-8">
        <h1 className="text-2xl font-semibold text-white">Watchlista</h1>
        <p className="mt-2 text-sm text-ink-400">
          Zapisane w tej przeglądarce. Identyfikator sesji to anonimowy UUID w localStorage, nie konto -
          wyczyszczenie danych strony kasuje listę.
        </p>
      </header>

      {watchlist.isPending && (
        <div className="space-y-3">
          <LineSkeleton className="h-24 w-full rounded-2xl" />
          <LineSkeleton className="h-24 w-full rounded-2xl" />
        </div>
      )}

      {watchlist.isError && <ErrorState error={watchlist.error} onRetry={() => watchlist.refetch()} />}

      {watchlist.data && items.length === 0 && (
        <EmptyState
          title="Jeszcze nic tu nie ma"
          hints={['Kliknij gwiazdkę przy rekomendacji, żeby zapisać repozytorium razem z notatką.']}
          action={
            <Link to="/" className="rounded-lg bg-sky-500 px-4 py-2 font-medium text-ink-950 hover:bg-sky-400">
              Znajdź rekomendacje
            </Link>
          }
        />
      )}

      <ul className="space-y-3">
        {items.map((item) => (
          <WatchlistRow key={item.id} item={item} />
        ))}
      </ul>
    </div>
  )
}

function WatchlistRow({ item }: { item: WatchlistItem }) {
  const [editing, setEditing] = useState(false)
  const [note, setNote] = useState(item.note ?? '')

  const update = useUpdateNote()
  const remove = useRemoveFromWatchlist()

  const save = () => {
    const trimmed = note.trim()
    update.mutate({ id: item.id, note: trimmed.length === 0 ? null : trimmed })
    setEditing(false)
  }

  const cancel = () => {
    setNote(item.note ?? '')
    setEditing(false)
  }

  return (
    <li className="rounded-2xl border border-ink-800 bg-ink-900/50 p-5">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <a
            href={item.repo?.htmlUrl ?? `https://github.com/${item.repoFullName}`}
            target="_blank"
            rel="noreferrer"
            className="font-semibold text-white hover:text-sky-300"
          >
            {item.repoFullName}
          </a>

          {item.repo ? (
            <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-400">
              <span>{formatStars(item.repo.stars)} ★</span>
              {item.repo.primaryLanguage && <span>{item.repo.primaryLanguage}</span>}
              <span>push {formatAgo(item.repo.lastPushedAt)}</span>
              {item.repo.healthScore !== null && <span>Health {Math.round(item.repo.healthScore)}</span>}
            </p>
          ) : (
            // Metadane dokładamy z cache'u, więc watchlista nie wydaje ani
            // jednego wywołania do GitHuba i działa po wyczerpaniu limitu.
            <p className="mt-1 text-xs text-ink-400">Metadanych nie ma w cache'u.</p>
          )}

          {item.repo?.description && <p className="mt-2 text-sm text-ink-400">{item.repo.description}</p>}
        </div>

        <button
          type="button"
          onClick={() => remove.mutate(item.id)}
          disabled={remove.isPending}
          className="shrink-0 rounded-lg border border-ink-800 px-3 py-1.5 text-sm text-ink-400 transition hover:border-rose-500/40 hover:text-rose-300"
        >
          Usuń
        </button>
      </div>

      <div className="mt-4">
        {editing ? (
          <div className="space-y-2">
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value.slice(0, maxNoteLength))}
              rows={3}
              autoFocus
              placeholder="Po co Ci to repo? Np. issue #412 wygląda na wieczorne zadanie."
              className="w-full rounded-lg border border-ink-700 bg-ink-900 px-3 py-2 text-sm text-ink-200 outline-none placeholder:text-ink-700 focus:border-sky-400/60"
            />
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={save}
                className="rounded-lg bg-sky-500 px-3 py-1.5 text-sm font-medium text-ink-950 transition hover:bg-sky-400"
              >
                Zapisz
              </button>
              <button
                type="button"
                onClick={cancel}
                className="rounded-lg border border-ink-700 px-3 py-1.5 text-sm text-ink-400 transition hover:text-ink-200"
              >
                Anuluj
              </button>
              <span className="ml-auto text-xs text-ink-700 tabular-nums">
                {note.length} / {maxNoteLength}
              </span>
            </div>
          </div>
        ) : (
          <button
            type="button"
            onClick={() => setEditing(true)}
            className="w-full rounded-lg border border-dashed border-ink-800 px-3 py-2 text-left text-sm transition hover:border-ink-700"
          >
            {item.note ? (
              <span className="text-ink-200">{item.note}</span>
            ) : (
              <span className="text-ink-700">Dodaj notatkę</span>
            )}
          </button>
        )}
      </div>
    </li>
  )
}
