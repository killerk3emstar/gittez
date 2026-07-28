import type { Issue } from '../api/types'

const difficultyLabel: Record<number, string> = {
  1: 'łatwe',
  2: 'średnie',
  3: 'trudniejsze',
}

const difficultyTone: Record<number, string> = {
  1: 'bg-emerald-500/15 text-emerald-300 border-emerald-500/30',
  2: 'bg-sky-500/15 text-sky-300 border-sky-500/30',
  3: 'bg-amber-500/15 text-amber-300 border-amber-500/30',
}

export function IssueChip({ issue }: { issue: Issue }) {
  return (
    <a
      href={issue.htmlUrl}
      target="_blank"
      rel="noreferrer"
      className="group flex items-start gap-2 rounded-lg border border-ink-800 bg-ink-900/60 px-3 py-2 text-sm transition hover:border-ink-700 hover:bg-ink-800/60"
    >
      <span
        // Ocena trudności to heurystyka z labeli, długości opisu i liczby
        // komentarzy - nie analiza kodu (SPEC §6.3).
        className={`mt-0.5 shrink-0 rounded border px-1.5 py-0.5 text-[0.65rem] font-medium ${difficultyTone[issue.difficulty]}`}
        title={`Szacowana trudność: ${difficultyLabel[issue.difficulty]} (heurystyka, nie analiza kodu)`}
      >
        {difficultyLabel[issue.difficulty]}
      </span>
      <span className="min-w-0 flex-1">
        <span className="line-clamp-2 text-ink-200 group-hover:text-white">{issue.title}</span>
        <span className="mt-0.5 block text-xs text-ink-400">
          #{issue.number}
          {issue.commentCount > 0 && ` · ${issue.commentCount} komentarzy`}
        </span>
      </span>
    </a>
  )
}
