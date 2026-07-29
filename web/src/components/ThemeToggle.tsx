import { useTheme, type ThemeChoice } from '../hooks/useTheme'

const description: Record<ThemeChoice, string> = {
  system: 'z ustawienia systemu',
  light: 'jasny',
  dark: 'ciemny',
}

// Trzy stany, nie dwa: bez pozycji "auto" pierwsze kliknięcie odbierałoby
// urządzeniu decyzję na zawsze.
export function ThemeToggle() {
  const { choice, cycle } = useTheme()

  return (
    <button
      type="button"
      onClick={cycle}
      title={`Motyw: ${description[choice]}. Kliknij, żeby zmienić.`}
      aria-label={`Motyw: ${description[choice]}`}
      className="flex items-center gap-1.5 rounded-chip border border-rule px-2 py-1 text-muted transition hover:border-rule-strong hover:text-ink"
    >
      <Glyph choice={choice} />
      <span className="label hidden lg:inline">{choice === 'system' ? 'auto' : description[choice]}</span>
    </button>
  )
}

function Glyph({ choice }: { choice: ThemeChoice }) {
  const common = {
    width: 14,
    height: 14,
    viewBox: '0 0 16 16',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 1.4,
    'aria-hidden': true,
  } as const

  if (choice === 'dark') {
    return (
      <svg {...common}>
        <path d="M13.2 9.6A5.6 5.6 0 0 1 6.4 2.8a5.6 5.6 0 1 0 6.8 6.8Z" strokeLinejoin="round" />
      </svg>
    )
  }

  if (choice === 'light') {
    return (
      <svg {...common}>
        <circle cx="8" cy="8" r="3.1" />
        <path d="M8 1.2v1.4M8 13.4v1.4M1.2 8h1.4M13.4 8h1.4M3.2 3.2l1 1M11.8 11.8l1 1M12.8 3.2l-1 1M4.2 11.8l-1 1" strokeLinecap="round" />
      </svg>
    )
  }

  return (
    <svg {...common}>
      <circle cx="8" cy="8" r="5.6" />
      <path d="M8 2.4a5.6 5.6 0 0 1 0 11.2Z" fill="currentColor" stroke="none" />
    </svg>
  )
}
