import type { Issue } from '../api/types'
import { formatComments } from '../lib/format'

const difficultyLabel: Record<number, string> = {
  1: 'łatwe',
  2: 'średnie',
  3: 'trudniejsze',
}

// Trudność to trzeci stopień tej samej skali, więc dostaje miarkę, nie kolor:
// kolory w tym interfejsie są zarezerwowane dla dwóch osi oceny.
function DifficultyMeter({ level }: { level: number }) {
  return (
    <span className="flex shrink-0 items-center gap-0.5" aria-hidden="true">
      {[1, 2, 3].map((step) => (
        <span key={step} className={`size-1.5 rounded-full ${step <= level ? 'bg-ink-soft' : 'bg-track'}`} />
      ))}
    </span>
  )
}

export function IssueChip({ issue }: { issue: Issue }) {
  return (
    <a
      href={issue.htmlUrl}
      target="_blank"
      rel="noreferrer"
      className="group -mx-2 flex items-start gap-2.5 border-t border-rule px-2 py-2 text-sm transition first:border-t-0 hover:bg-sunk"
    >
      <span
        className="mt-1.5"
        // Ocena trudności to heurystyka z labeli, długości opisu i liczby
        // komentarzy - nie analiza kodu (SPEC §6.3).
        title={`Szacowana trudność: ${difficultyLabel[issue.difficulty]} (heurystyka, nie analiza kodu)`}
      >
        <DifficultyMeter level={issue.difficulty} />
      </span>

      <span className="min-w-0 flex-1">
        <span className="line-clamp-2 text-ink-soft transition group-hover:text-ink">{issue.title}</span>
        <span className="num mt-0.5 block text-xs text-muted">
          {difficultyLabel[issue.difficulty]}
          <span className="font-mono"> · #{issue.number}</span>
          {issue.commentCount > 0 && ` · ${formatComments(issue.commentCount)}`}
        </span>
      </span>
    </a>
  )
}
