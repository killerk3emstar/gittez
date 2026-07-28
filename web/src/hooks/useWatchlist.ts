import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError, type Fresh } from '../api/client'
import type { WatchlistItem } from '../api/types'

const key = ['watchlist']

type Cached = Fresh<WatchlistItem[]> | undefined

export function useWatchlist() {
  return useQuery({
    queryKey: key,
    queryFn: ({ signal }) => api.watchlist.list(signal),
    staleTime: 30 * 1000,
  })
}

// Zbiór nazw repozytoriów już zapisanych - gwiazdka na karcie wyników czyta
// stąd, żeby nie strzelać osobno dla każdej z dziesięciu kart.
export function useWatchedNames(): Set<string> {
  const { data } = useWatchlist()

  return new Set((data?.data ?? []).map((item) => item.repoFullName.toLowerCase()))
}

export function useAddToWatchlist() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: ({ repoFullName, note }: { repoFullName: string; note?: string | null }) =>
      api.watchlist.add(repoFullName, note),

    onMutate: async ({ repoFullName, note }) => {
      await client.cancelQueries({ queryKey: key })
      const previous = client.getQueryData<Cached>(key)

      // Ujemne id żyje tylko do odpowiedzi serwera; realne id przychodzi z 201.
      const optimistic: WatchlistItem = {
        id: -Date.now(),
        repoFullName,
        note: note ?? null,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        repo: null,
      }

      client.setQueryData<Cached>(key, (current) =>
        current ? { ...current, data: [optimistic, ...current.data] } : current,
      )

      return { previous }
    },

    onError: (error, _variables, context) => {
      if (context?.previous) client.setQueryData(key, context.previous)

      // 409 znaczy, że repo już tam jest - stan po odświeżeniu i tak będzie
      // poprawny, więc nie ma czego cofać poza optymistycznym wierszem.
      if (error instanceof ApiError && error.code === 'already-on-watchlist') return
    },

    onSettled: () => client.invalidateQueries({ queryKey: key }),
  })
}

export function useUpdateNote() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: ({ id, note }: { id: number; note: string | null }) => api.watchlist.updateNote(id, note),

    onMutate: async ({ id, note }) => {
      await client.cancelQueries({ queryKey: key })
      const previous = client.getQueryData<Cached>(key)

      client.setQueryData<Cached>(key, (current) =>
        current
          ? {
              ...current,
              data: current.data.map((item) => (item.id === id ? { ...item, note } : item)),
            }
          : current,
      )

      return { previous }
    },

    onError: (_error, _variables, context) => {
      if (context?.previous) client.setQueryData(key, context.previous)
    },

    onSettled: () => client.invalidateQueries({ queryKey: key }),
  })
}

export function useRemoveFromWatchlist() {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (id: number) => api.watchlist.remove(id),

    onMutate: async (id) => {
      await client.cancelQueries({ queryKey: key })
      const previous = client.getQueryData<Cached>(key)

      client.setQueryData<Cached>(key, (current) =>
        current ? { ...current, data: current.data.filter((item) => item.id !== id) } : current,
      )

      return { previous }
    },

    onError: (_error, _variables, context) => {
      if (context?.previous) client.setQueryData(key, context.previous)
    },

    onSettled: () => client.invalidateQueries({ queryKey: key }),
  })
}
