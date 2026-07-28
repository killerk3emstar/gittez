import { ApiError } from '../api/client'

// Kod z ProblemDetails.type niesie więcej niż status: 503 po wyczerpaniu limitu
// i 503 po odrzuconym tokenie wymagają zupełnie innej rady (SPEC §7.3).
export function describeApiError(error: unknown): { title: string; detail: string } {
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

    case 'already-on-watchlist':
      return { title: 'To repo już tam jest', detail: 'Znajdziesz je na watchliście.' }

    case 'watchlist-full':
      return {
        title: 'Watchlista jest pełna',
        detail: 'Usuń kilka zapisanych repozytoriów, żeby zrobić miejsce na nowe.',
      }

    case 'note-too-long':
      return { title: 'Notatka jest za długa', detail: 'Zmieść się w 500 znakach.' }

    case 'watchlist-item-not-found':
      return { title: 'Nie ma już takiej pozycji', detail: 'Mogła zostać usunięta w innej karcie.' }

    case 'missing-session':
      return {
        title: 'Brak identyfikatora sesji',
        detail: 'Odśwież stronę - identyfikator watchlisty jest tworzony w localStorage.',
      }

    default:
      return { title: 'Coś poszło nie tak', detail: error.title }
  }
}
