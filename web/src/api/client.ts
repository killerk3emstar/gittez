import { getSessionId } from '../hooks/useSession'
import type {
  Health,
  Profile,
  RecommendationQuery,
  RecommendationsResponse,
  WatchlistItem,
} from './types'

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

// Kod z ProblemDetails.type, nie sam status: backend rozróżnia sytuacje, które
// front pokazuje inaczej (brak loginu vs pusty profil vs wyczerpany limit).
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    readonly title: string,
    readonly retryAfterSeconds: number | null,
  ) {
    super(title)
    this.name = 'ApiError'
  }
}

// Odpowiedź z cache'u mimo wygasłego TTL jest poprawnym wynikiem, nie błędem -
// niesie tylko nagłówek, na który front reaguje bannerem (SPEC §7.3).
export type Fresh<T> = { data: T; isStale: boolean }

type RequestOptions = {
  method?: string
  body?: unknown
  signal?: AbortSignal
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<Fresh<T>> {
  const headers: Record<string, string> = { 'X-Session-Id': getSessionId() }
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'

  const response = await fetch(`${baseUrl}${path}`, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  })

  if (!response.ok) throw await toError(response)

  const isStale = response.headers.get('X-Data-Stale') === 'true'
  const data = response.status === 204 ? (undefined as T) : ((await response.json()) as T)

  return { data, isStale }
}

async function toError(response: Response): Promise<ApiError> {
  const retryAfter = Number(response.headers.get('Retry-After'))

  let code = `http-${response.status}`
  let title = 'Coś poszło nie tak'

  try {
    const problem = (await response.json()) as { type?: string; title?: string; detail?: string }
    if (problem.type) code = problem.type
    if (problem.detail || problem.title) title = problem.detail ?? problem.title!
  } catch {
    // Odpowiedź bez ProblemDetails (np. błąd proxy) zostaje z kodem po statusie.
  }

  return new ApiError(response.status, code, title, Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : null)
}

export const api = {
  profile: (login: string, signal?: AbortSignal) =>
    request<Profile>(`/api/profile/${encodeURIComponent(login)}`, { signal }),

  recommendations: (query: RecommendationQuery, signal?: AbortSignal) => {
    const params = new URLSearchParams({
      login: query.login,
      targetStars: String(query.targetStars),
    })

    if (query.languages.length > 0) params.set('languages', query.languages.join(','))
    if (query.maxDifficulty !== null) params.set('maxDifficulty', String(query.maxDifficulty))

    return request<RecommendationsResponse>(`/api/recommendations?${params}`, { signal })
  },

  health: (signal?: AbortSignal) => request<Health>('/api/health', { signal }),

  watchlist: {
    list: (signal?: AbortSignal) => request<WatchlistItem[]>('/api/watchlist', { signal }),

    add: (repoFullName: string, note?: string | null) =>
      request<WatchlistItem>('/api/watchlist', {
        method: 'POST',
        body: { repoFullName, note: note ?? null },
      }),

    updateNote: (id: number, note: string | null) =>
      request<WatchlistItem>(`/api/watchlist/${id}`, { method: 'PATCH', body: { note } }),

    remove: (id: number) => request<void>(`/api/watchlist/${id}`, { method: 'DELETE' }),
  },
}
