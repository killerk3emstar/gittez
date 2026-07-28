// DTO 1:1 z kontraktami backendu (SPEC §7). System.Text.Json serializuje
// rekordy do camelCase, więc nazwy pól są tu takie same jak w C#.

export type ProfileLanguage = {
  name: string
  ownedRepos: number
  contributedRepos: number
  rank: number
}

export type Profile = {
  login: string
  publicRepoCount: number
  medianSizeKb: number
  languages: ProfileLanguage[]
  interests: string[]
  computedAt: string
}

// points === null to "za mało danych": wynik procentuje się po dostępnych
// komponentach, a UI pokazuje szary pasek zamiast zera (SPEC §6.2).
export type ScoreComponent = {
  key: string
  label: string
  points: number | null
  maxPoints: number
  rawValue: string
  explanation: string
  isSampled: boolean
}

export type Issue = {
  number: number
  title: string
  htmlUrl: string
  labels: string[]
  commentCount: number
  difficulty: 1 | 2 | 3
  updatedAt: string
}

export type DataFreshness = {
  repo: string
  health: string | null
}

export type Recommendation = {
  fullName: string
  description: string | null
  htmlUrl: string
  stars: number
  primaryLanguage: string | null
  topics: string[]
  lastPushedAt: string
  matchScore: number
  healthScore: number | null
  // Tylko do ustalenia kolejności - na karcie nie pojawia się jako liczba (SPEC §9).
  finalScore: number
  matchBreakdown: ScoreComponent[]
  healthBreakdown: ScoreComponent[]
  issues: Issue[]
  dataFreshness: DataFreshness
}

export type RecommendationsResponse = {
  items: Recommendation[]
  hints: string[]
}

export type WatchlistRepo = {
  description: string | null
  htmlUrl: string
  stars: number
  primaryLanguage: string | null
  lastPushedAt: string
  healthScore: number | null
}

export type WatchlistItem = {
  id: number
  repoFullName: string
  note: string | null
  createdAt: string
  updatedAt: string
  repo: WatchlistRepo | null
}

export type RateLimitPool = {
  remaining: number
  limit: number
  used: number
  resetAt: string
}

export type Health = {
  status: string
  database: { canConnect: boolean; error: string | null }
  rateLimit: { core: RateLimitPool | null; search: RateLimitPool | null } | null
}

export type RecommendationQuery = {
  login: string
  languages: string[]
  targetStars: number
  maxDifficulty: number | null
}
