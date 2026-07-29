import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'

export function useProfile(login: string) {
  const trimmed = login.trim()

  return useQuery({
    queryKey: ['profile', trimmed.toLowerCase()],
    queryFn: ({ signal }) => api.profile(trimmed, signal),
    enabled: trimmed.length > 0,
    // Profil w cache'u backendu żyje 24 h, więc powtórny strzał z tego samego
    // ekranu i tak nie dołoży nic nowego.
    staleTime: 5 * 60 * 1000,
    retry: false,
  })
}
