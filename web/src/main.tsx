import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import '@fontsource-variable/archivo/wdth.css'
import '@fontsource-variable/geist-mono'
import App from './App'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Ponawianie po 404 na nieistniejącym loginie albo po wyczerpanym limicie
      // tylko zjada budżet - błędy z API są rozstrzygnięciem, nie usterką sieci.
      retry: false,
      refetchOnWindowFocus: false,
    },
  },
})

// Licznik limitu czyta stan z pamięci procesu API, a ten zmienia się dopiero
// przy wywołaniu GitHuba. Bez tego nagłówek pokazywałby budżet sprzed
// wyszukiwania aż do przeładowania strony - również gdy przebieg skończył się
// błędem, bo odbicie po limicie zjada tyle samo, co odpowiedź.
queryClient.getQueryCache().subscribe((event) => {
  if (event.type !== 'updated') return
  if (event.action.type !== 'success' && event.action.type !== 'error') return

  const scope = event.query.queryKey[0]
  if (scope === 'profile' || scope === 'recommendations') {
    queryClient.invalidateQueries({ queryKey: ['health'] })
  }
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
