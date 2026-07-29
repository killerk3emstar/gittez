type Props = {
  computedAt?: string | null
}

function hoursAgo(iso: string): number {
  return Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 3_600_000))
}

// Limit GitHuba się wyczerpał, ale cache miał czym poratować: serwujemy stare
// dane z bannerem, bo puste demo z komunikatem o błędzie jest gorsze (SPEC §7.3).
export function StaleBanner({ computedAt }: Props) {
  const age = computedAt ? hoursAgo(computedAt) : null

  return (
    <div className="rounded-chip border border-l-2 border-rule border-l-amber bg-panel px-4 py-3 text-sm">
      <p className="label text-amber">Dane z cache'u</p>
      <p className="mt-1.5 text-ink-soft">
        {age === null
          ? "Pokazujemy zapis z cache'u."
          : age === 0
            ? 'Zapis sprzed niecałej godziny.'
            : `Zapis sprzed ${age} h.`}{' '}
        <span className="text-muted">
          Limit GitHub API jest wyczerpany, więc pokazujemy ostatnie, co mamy, zamiast pustego ekranu.
        </span>
      </p>
    </div>
  )
}
