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
    <div className="flex items-start gap-3 rounded-xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-100">
      <span aria-hidden="true" className="mt-0.5 text-base leading-none">
        !
      </span>
      <p>
        <span className="font-medium">
          {age === null
            ? "Dane pochodzą z cache'u."
            : age === 0
              ? "Dane pochodzą z cache'u sprzed niecałej godziny."
              : `Dane sprzed ${age} h.`}
        </span>{' '}
        Limit GitHub API jest wyczerpany, więc pokazujemy ostatnie, co mamy zapisane, zamiast pustego ekranu.
      </p>
    </div>
  )
}
