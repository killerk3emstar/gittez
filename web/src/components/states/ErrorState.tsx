import { describeApiError } from '../../lib/errors'

type Props = {
  error: unknown
  onRetry?: () => void
}

export function ErrorState({ error, onRetry }: Props) {
  const { title, detail } = describeApiError(error)

  return (
    <div className="rounded-panel border border-l-2 border-rule border-l-rust bg-panel px-6 py-8">
      <p className="label text-rust">Nie udało się</p>
      <h3 className="display mt-2 text-lg text-ink">{title}</h3>
      <p className="mt-2 max-w-lg text-sm leading-relaxed text-muted">{detail}</p>

      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-5 rounded-chip border border-rule px-3 py-1.5 text-sm text-ink transition hover:border-rule-strong hover:bg-sunk"
        >
          Spróbuj ponownie
        </button>
      )}
    </div>
  )
}

// Nieudana mutacja nie może zniknąć bez śladu: optymistyczny wiersz wycofuje
// się sam, więc bez tego kliknięcie w gwiazdkę wygląda jak brak reakcji.
export function InlineError({ error, onDismiss }: { error: unknown; onDismiss?: () => void }) {
  const { title, detail } = describeApiError(error)

  return (
    <div className="flex items-start gap-3 rounded-chip border border-l-2 border-rule border-l-rust bg-panel px-4 py-3 text-sm">
      <p className="flex-1 text-ink-soft">
        <span className="font-medium text-ink">{title}.</span> {detail}
      </p>

      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          aria-label="Zamknij komunikat"
          className="shrink-0 text-muted transition hover:text-ink"
        >
          ✕
        </button>
      )}
    </div>
  )
}
