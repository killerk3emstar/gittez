import { ApiError } from '../../api/client'

type Props = {
  error: unknown
  onRetry?: () => void
}

// Kod z ProblemDetails.type niesie więcej niż status: 503 po wyczerpaniu limitu
// i 503 po odrzuconym tokenie wymagają zupełnie innej rady (SPEC §7.3).
function describe(error: unknown): { title: string; detail: string } {
  if (!(error instanceof ApiError)) {
    return {
      title: 'Nie udało się połączyć z API',
      detail: 'Sprawdź, czy backend odpowiada pod /api/health.',
    }
  }

  switch (error.code) {
    case 'github-user-not-found':
      return { title: 'Nie ma takiego użytkownika', detail: 'Sprawdź pisownię loginu GitHub.' }

    case 'insufficient-profile-data':
      return {
        title: 'Za mało danych w tym profilu',
        detail: 'Ten login nie ma publicznych repozytoriów. Wpisz języki ręcznie albo podaj inny login.',
      }

    case 'github-rate-limited': {
      const minutes = error.retryAfterSeconds ? Math.ceil(error.retryAfterSeconds / 60) : null

      return {
        title: 'Limit GitHub API wyczerpany, a cache jest pusty',
        detail: minutes
          ? `Limit odnowi się za około ${minutes} min. Z tokenem w .env limit to 5000 zapytań na godzinę zamiast 60.`
          : 'Z tokenem w .env limit to 5000 zapytań na godzinę zamiast 60.',
      }
    }

    case 'github-unavailable':
      return {
        title: 'GitHub odrzucił zapytanie',
        detail: 'Token jest nieprawidłowy albo wygasł. Sprawdź GITHUB_TOKEN w .env.',
      }

    case 'watchlist-full':
      return {
        title: 'Watchlista jest pełna',
        detail: 'Usuń kilka zapisanych repozytoriów, żeby zrobić miejsce na nowe.',
      }

    case 'missing-session':
      return {
        title: 'Brak identyfikatora sesji',
        detail: 'Odśwież stronę - identyfikator watchlisty jest tworzony w localStorage.',
      }

    default:
      return { title: 'Coś poszło nie tak', detail: error.title }
  }
}

export function ErrorState({ error, onRetry }: Props) {
  const { title, detail } = describe(error)

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
