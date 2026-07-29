import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'

// Odpytywanie co minutę dopisuje się do unieważnienia po każdym przebiegu
// (main.tsx): samo unieważnienie nie złapie odnowienia limitu, które dzieje
// się po stronie GitHuba, gdy nikt nic nie klika.
export function useHealth() {
  return useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => api.health(signal),
    staleTime: 15 * 1000,
    refetchInterval: 60 * 1000,
    refetchOnWindowFocus: true,
    retry: false,
  })
}
