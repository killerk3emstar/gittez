import { useEffect, useState } from 'react'

const storageKey = 'gittez.theme'

export type ThemeChoice = 'system' | 'light' | 'dark'

function storedChoice(): ThemeChoice {
  try {
    const value = localStorage.getItem(storageKey)
    return value === 'light' || value === 'dark' ? value : 'system'
  } catch {
    // localStorage bywa zablokowany w trybie prywatnym - zostaje ustawienie systemu.
    return 'system'
  }
}

function systemPrefersDark(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(prefers-color-scheme: dark)').matches
}

// Domyślnie motyw idzie za urządzeniem i reaguje na zmianę ustawienia bez
// przeładowania. Ręczny wybór jest nadpisaniem, nie zastąpieniem: powrót do
// "auto" oddaje decyzję systemowi.
export function useTheme() {
  const [choice, setChoice] = useState<ThemeChoice>(storedChoice)
  const [systemDark, setSystemDark] = useState(systemPrefersDark)

  useEffect(() => {
    const query = matchMedia('(prefers-color-scheme: dark)')
    const onChange = (event: MediaQueryListEvent) => setSystemDark(event.matches)

    query.addEventListener('change', onChange)
    return () => query.removeEventListener('change', onChange)
  }, [])

  const resolved = choice === 'system' ? (systemDark ? 'dark' : 'light') : choice

  useEffect(() => {
    document.documentElement.dataset.theme = resolved
  }, [resolved])

  const cycle = () => {
    const next: ThemeChoice = choice === 'system' ? 'light' : choice === 'light' ? 'dark' : 'system'
    setChoice(next)

    try {
      if (next === 'system') localStorage.removeItem(storageKey)
      else localStorage.setItem(storageKey, next)
    } catch {
      // Wybór zadziała do końca sesji, tylko nie przetrwa przeładowania.
    }
  }

  return { choice, resolved, cycle }
}
