import type { Recommendation, ScoreComponent } from '../api/types'
import { formatAgo, formatStars } from '../lib/format'
import { HealthBadge } from './HealthBadge'
import { IssueChip } from './IssueChip'
import { ScoreRing } from './ScoreRing'

type Props = {
  item: Recommendation
  highlight: ScoreComponent | undefined
  isWatched: boolean
  isSaving: boolean
  onToggleWatch: () => void
  onExplain: () => void
}

export function RepoCard({ item, highlight, isWatched, isSaving, onToggleWatch, onExplain }: Props) {
  return (
    <article className="flex flex-col rounded-2xl border border-ink-800 bg-ink-900/50 p-5 transition hover:border-ink-700">
      <header className="flex items-start gap-4">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <a
              href={item.htmlUrl}
              target="_blank"
              rel="noreferrer"
              className="truncate font-semibold text-white hover:text-sky-300"
            >
              {item.fullName}
            </a>
            <button
              type="button"
              onClick={onToggleWatch}
              disabled={isSaving || isWatched}
              aria-label={isWatched ? 'Jest na watchliście' : 'Zapisz na watchliście'}
              title={isWatched ? 'Jest na watchliście' : 'Zapisz na watchliście'}
              className={`shrink-0 rounded-md px-1.5 py-0.5 text-lg leading-none transition disabled:cursor-default ${
                isWatched ? 'text-amber-300' : 'text-ink-700 hover:text-amber-300'
              }`}
            >
              {isWatched ? '★' : '☆'}
            </button>
          </div>

          <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-400">
            <span>{formatStars(item.stars)} ★</span>
            {item.primaryLanguage && <span>{item.primaryLanguage}</span>}
            <span>push {formatAgo(item.lastPushedAt)}</span>
          </p>

          {item.description && <p className="mt-2 line-clamp-2 text-sm text-ink-400">{item.description}</p>}
        </div>

        <ScoreRing value={item.matchScore} label="Match" />
      </header>

      <div className="mt-4 flex flex-wrap items-center gap-2">
        <HealthBadge score={item.healthScore} breakdown={item.healthBreakdown} />
        {item.topics.slice(0, 3).map((topic) => (
          <span key={topic} className="rounded-full border border-ink-800 px-2.5 py-1 text-xs text-ink-400">
            {topic}
          </span>
        ))}
      </div>

      {highlight && (
        // Zdanie bierzemy z ScoreComponent.Explanation, więc na karcie nie
        // powstaje żadna nowa logika opisowa (SPEC §9).
        <p className="mt-4 border-l-2 border-sky-400/50 pl-3 text-sm text-ink-200">
          {highlight.explanation}
        </p>
      )}

      <div className="mt-4 space-y-2">
        {item.issues.slice(0, 3).map((issue) => (
          <IssueChip key={issue.number} issue={issue} />
        ))}
      </div>

      <footer className="mt-4 flex items-center justify-between pt-1">
        <button
          type="button"
          onClick={onExplain}
          className="rounded-lg border border-ink-700 px-3 py-1.5 text-sm text-ink-200 transition hover:bg-ink-800"
        >
          Dlaczego?
        </button>

        <a
          href={item.htmlUrl}
          target="_blank"
          rel="noreferrer"
          className="text-sm text-ink-400 transition hover:text-ink-200"
        >
          Otwórz na GitHubie
        </a>
      </footer>
    </article>
  )
}
