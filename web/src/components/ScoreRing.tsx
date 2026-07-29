type Props = {
  value: number
  label: string
  size?: number
}

// Pierścień pokazuje wyłącznie Match. Wynik końcowy zostaje poza kartą: w
// widocznej dziesiątce mieści się w 6,8 punktu, więc duża liczba sugerowałaby
// precyzję, której nie ma (SPEC §9).
export function ScoreRing({ value, label, size = 64 }: Props) {
  const stroke = 6
  const radius = (size - stroke) / 2
  const circumference = 2 * Math.PI * radius
  const filled = (Math.max(0, Math.min(100, value)) / 100) * circumference

  return (
    <div className="flex flex-col items-center gap-1">
      <div className="relative" style={{ width: size, height: size }}>
        <svg width={size} height={size} className="-rotate-90" aria-hidden="true">
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke="currentColor"
            strokeWidth={stroke}
            className="text-ink-800"
          />
          <circle
            cx={size / 2}
            cy={size / 2}
            r={radius}
            fill="none"
            stroke="currentColor"
            strokeWidth={stroke}
            strokeLinecap="round"
            strokeDasharray={`${filled} ${circumference - filled}`}
            className="text-sky-400"
          />
        </svg>
        <span className="absolute inset-0 flex items-center justify-center text-lg font-semibold text-white tabular-nums">
          {Math.round(value)}
        </span>
      </div>
      <span className="text-[0.65rem] font-medium uppercase tracking-wider text-ink-400">{label}</span>
      <span className="sr-only">
        {label}: {value.toFixed(1)} na 100
      </span>
    </div>
  )
}
