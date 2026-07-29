import type { Recommendation, ScoreComponent } from '../api/types'
import { formatAgo, formatStars } from '../lib/format'
import type { Band } from '../lib/highlight'
import { weakestComponent } from '../lib/highlight'
import { IssueChip } from './IssueChip'
import { RailRow } from './ScoreRail'

type Props = {
  item: Recommendation
  highlight: ScoreComponent | undefined
  bands: { match: Band | null; health: Band | null }
  isWatched: boolean
  isSaving: boolean
  onToggleWatch: () => void
  onExplain: () => void
}

export function RepoCard({ item, highlight, bands, isWatched, isSaving, onToggleWatch, onExplain }: Props) {
  const [owner, name] = splitName(item.fullName)
  const weakest = item.healthScore !== null && item.healthScore < 45 ? weakestComponent(item.healthBreakdown) : null

  return (
    // min-w-0 na samej karcie, nie tylko na kolumnie: nazwa repo ma nowrap od
    // truncate, więc bez tego karta nie zejdzie poniżej swojego min-content w
    // żadnym kontenerze, w który ją włożymy.
    <article className="flex min-w-0 flex-col rounded-panel border border-rule bg-panel p-5 transition hover:border-rule-strong">
      <div className="flex items-start gap-3">
        <div className="min-w-0 flex-1">
          <a
            href={item.htmlUrl}
            target="_blank"
            rel="noreferrer"
            className="block truncate font-mono text-sm font-medium text-ink underline decoration-transparent underline-offset-4 transition hover:decoration-rule-strong"
          >
            <span className="text-muted">{owner}/</span>
            {name}
          </a>

          <p className="num mt-1 flex flex-wrap items-center gap-x-2.5 gap-y-1 text-xs text-muted">
            <span>{formatStars(item.stars)} ★</span>
            {item.primaryLanguage && <span>{item.primaryLanguage}</span>}
            <span>push {formatAgo(item.lastPushedAt)}</span>
          </p>
        </div>

        <WatchButton isWatched={isWatched} isSaving={isSaving} onToggle={onToggleWatch} />
      </div>

      {item.description && <p className="mt-3 line-clamp-2 text-sm leading-snug text-ink-soft">{item.description}</p>}

      <div className="mt-5 space-y-3">
        <RailRow
          label="Match"
          axis="match"
          value={item.matchScore}
          band={bands.match}
          readout={Math.round(item.matchScore)}
        />
        <RailRow
          label="Health"
          axis={item.healthScore !== null && item.healthScore < 45 ? 'danger' : 'health'}
          value={item.healthScore}
          band={bands.health}
          readout={item.healthScore === null ? 'za mało danych' : Math.round(item.healthScore)}
          note={weakest ? `${weakest.label}: ${weakest.rawValue}` : undefined}
        />
      </div>

      {highlight && (
        // Zdanie bierzemy z ScoreComponent.Explanation, więc na karcie nie
        // powstaje żadna nowa logika opisowa (SPEC §9).
        <p className="mt-4 border-l-2 border-copper pl-3 text-sm leading-snug text-ink-soft">
          {highlight.explanation}
        </p>
      )}

      {item.topics.length > 0 && (
        <p className="mt-4 flex flex-wrap gap-x-2 gap-y-1 font-mono text-xs text-muted">
          {item.topics.slice(0, 4).map((topic) => (
            <span key={topic}>#{topic}</span>
          ))}
        </p>
      )}

      <div className="mt-4 border-t border-rule pt-1">
        {item.issues.slice(0, 3).map((issue) => (
          <IssueChip key={issue.number} issue={issue} />
        ))}
      </div>

      <div className="mt-auto flex items-center justify-between gap-3 pt-4">
        <button
          type="button"
          onClick={onExplain}
          className="rounded-chip border border-rule px-3 py-1.5 text-sm text-ink transition hover:border-rule-strong hover:bg-sunk"
        >
          Skąd te liczby?
        </button>

        <a
          href={item.htmlUrl}
          target="_blank"
          rel="noreferrer"
          className="text-sm text-muted transition hover:text-ink"
        >
          Otwórz na GitHubie
        </a>
      </div>
    </article>
  )
}

function splitName(fullName: string): [string, string] {
  const slash = fullName.indexOf('/')
  return slash === -1 ? ['', fullName] : [fullName.slice(0, slash), fullName.slice(slash + 1)]
}

function WatchButton({
  isWatched,
  isSaving,
  onToggle,
}: {
  isWatched: boolean
  isSaving: boolean
  onToggle: () => void
}) {
  const title = isWatched ? 'Jest na watchliście' : 'Zapisz na watchliście'

  return (
    <button
      type="button"
      onClick={onToggle}
      disabled={isSaving || isWatched}
      aria-label={title}
      title={title}
      className={`shrink-0 rounded-chip border p-1.5 transition disabled:cursor-default ${
        isWatched ? 'border-rule bg-sunk text-ink' : 'border-transparent text-faint hover:border-rule hover:text-ink'
      }`}
    >
      <svg
        width="15"
        height="15"
        viewBox="0 0 16 16"
        fill={isWatched ? 'currentColor' : 'none'}
        stroke="currentColor"
        strokeWidth="1.3"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <path d="M8 1.8l1.85 3.9 4.15.6-3 3 .71 4.3L8 11.55 4.29 13.6 5 9.3l-3-3 4.15-.6z" />
      </svg>
    </button>
  )
}
