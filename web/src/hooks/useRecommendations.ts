import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { RecommendationQuery } from '../api/types'

export function useRecommendations(query: RecommendationQuery | null) {
  return useQuery({
    queryKey: ['recommendations', query],
    queryFn: ({ signal }) => api.recommendations(query!, signal),
    enabled: query !== null,
    // Świeży przebieg to ~100 wywołań do GitHuba i 6-8 sekund (SPEC §2.2) -
    // powrót z watchlisty nie ma go powtarzać.
    staleTime: 10 * 60 * 1000,
    retry: false,
  })
}
