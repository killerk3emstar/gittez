import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { WatchlistItem } from '../api/types'
import { EmptyState } from '../components/states/EmptyState'
import { ErrorState, InlineError } from '../components/states/ErrorState'
import { LineSkeleton } from '../components/states/Skeleton'
import { useRemoveFromWatchlist, useUpdateNote, useWatchlist } from '../hooks/useWatchlist'
import { formatAgo, formatStars } from '../lib/format'
import { buttonGhost, buttonPrimary } from '../lib/ui'

const maxNoteLength = 500

export function Watchlist() {
  const watchlist = useWatchlist()
  const items = watchlist.data?.data ?? []

  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <header className="border-b border-rule pb-6">
        <p className="label text-muted">Zapisane</p>
        <h1 className="display mt-2 text-2xl text-ink">Watchlista</h1>
        <p className="mt-3 max-w-xl text-sm leading-relaxed text-muted">
          Trzymana w tej przeglądarce. Identyfikator sesji to anonimowy UUID w localStorage, nie konto:
          wyczyszczenie danych strony kasuje listę.
        </p>
      </header>

      <div className="mt-8">
        {watchlist.isPending && (
          <div className="space-y-3">
            <LineSkeleton className="h-28 w-full rounded-panel" />
            <LineSkeleton className="h-28 w-full rounded-panel" />
          </div>
        )}

        {watchlist.isError && <ErrorState error={watchlist.error} onRetry={() => watchlist.refetch()} />}

        {watchlist.data && items.length === 0 && (
          <EmptyState
            title="Jeszcze nic tu nie ma"
            hints={['Kliknij gwiazdkę przy rekomendacji, żeby zapisać repozytorium razem z notatką.']}
            action={
              <Link to="/" className={buttonPrimary}>
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
    <li className="rounded-panel border border-rule bg-panel p-5">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <a
            href={item.repo?.htmlUrl ?? `https://github.com/${item.repoFullName}`}
            target="_blank"
            rel="noreferrer"
            className="font-mono text-sm font-medium text-ink underline decoration-transparent underline-offset-4 transition hover:decoration-rule-strong"
          >
            {item.repoFullName}
          </a>

          {item.repo ? (
            <p className="num mt-1.5 flex flex-wrap items-center gap-x-2.5 gap-y-1 text-xs text-muted">
              <span>{formatStars(item.repo.stars)} ★</span>
              {item.repo.primaryLanguage && <span>{item.repo.primaryLanguage}</span>}
              <span>push {formatAgo(item.repo.lastPushedAt)}</span>
              {item.repo.healthScore !== null && (
                <span className="text-patina-ink">Health {Math.round(item.repo.healthScore)}</span>
              )}
            </p>
          ) : (
            // Metadane dokładamy z cache'u, więc watchlista nie wydaje ani
            // jednego wywołania do GitHuba i działa po wyczerpaniu limitu.
            <p className="mt-1.5 text-xs text-muted">Metadanych nie ma w cache'u.</p>
          )}

          {item.repo?.description && (
            <p className="mt-2 text-sm leading-snug text-ink-soft">{item.repo.description}</p>
          )}
        </div>

        <button
          type="button"
          onClick={() => remove.mutate(item.id)}
          disabled={remove.isPending}
          className="shrink-0 rounded-chip border border-rule px-3 py-1.5 text-sm text-muted transition hover:border-rust hover:text-rust"
        >
          Usuń
        </button>
      </div>

      {(update.isError || remove.isError) && (
        <div className="mt-4">
          <InlineError
            error={update.error ?? remove.error}
            onDismiss={() => {
              update.reset()
              remove.reset()
            }}
          />
        </div>
      )}

      <div className="mt-4">
        {editing ? (
          <div className="space-y-2">
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value.slice(0, maxNoteLength))}
              rows={3}
              autoFocus
              placeholder="Po co Ci to repo? Np. issue #412 wygląda na wieczorne zadanie."
              className="w-full rounded-chip border border-rule bg-panel px-3 py-2 text-sm text-ink outline-none transition placeholder:text-faint focus:border-rule-strong"
            />
            <div className="flex items-center gap-2">
              <button type="button" onClick={save} className={buttonGhost}>
                Zapisz notatkę
              </button>
              <button type="button" onClick={cancel} className="px-2 py-1.5 text-sm text-muted transition hover:text-ink">
                Anuluj
              </button>
              <span className="num ml-auto text-xs text-muted">
                {note.length} / {maxNoteLength}
              </span>
            </div>
          </div>
        ) : (
          <button
            type="button"
            onClick={() => setEditing(true)}
            className="w-full rounded-chip border border-dashed border-rule px-3 py-2 text-left text-sm transition hover:border-rule-strong hover:bg-sunk"
          >
            {item.note ? <span className="text-ink-soft">{item.note}</span> : <span className="text-muted">Dodaj notatkę</span>}
          </button>
        )}
      </div>
    </li>
  )
}
