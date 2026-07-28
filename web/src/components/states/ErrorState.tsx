import { describeApiError } from '../../lib/errors'

type Props = {
  error: unknown
  onRetry?: () => void
}

export function ErrorState({ error, onRetry }: Props) {
  const { title, detail } = describeApiError(error)

  return (
    <div className="rounded-2xl border border-rose-500/30 bg-rose-500/5 px-6 py-8 text-center">
      <h3 className="text-lg font-semibold text-rose-200">{title}</h3>
      <p className="mx-auto mt-2 max-w-md text-sm text-ink-400">{detail}</p>

      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-5 rounded-lg border border-rose-400/40 bg-rose-500/10 px-4 py-2 text-sm font-medium text-rose-100 transition hover:bg-rose-500/20"
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
    <div className="flex items-start gap-3 rounded-xl border border-rose-500/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-100">
      <p className="flex-1">
        <span className="font-medium">{title}.</span> {detail}
      </p>

      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          aria-label="Zamknij komunikat"
          className="shrink-0 text-rose-200/70 transition hover:text-rose-100"
        >
          ✕
        </button>
      )}
    </div>
  )
}
