const storageKey = 'gitbounty.sessionId'

// Anonimowy UUID, nie uwierzytelnienie: da się go podrobić, ale nie chroni
// niczego wrażliwego (SPEC §7). Wiersz w bazie powstaje przy pierwszym zapisie.
export function getSessionId(): string {
  const stored = localStorage.getItem(storageKey)
  if (stored) return stored

  const created = randomUuid()
  localStorage.setItem(storageKey, created)

  return created
}

// crypto.randomUUID istnieje wyłącznie w bezpiecznym kontekście, więc demo
// otwarte po http z adresu w sieci lokalnej wywaliłoby się na każdym żądaniu.
function randomUuid(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID()

  const bytes = new Uint8Array(16)
  crypto.getRandomValues(bytes)
  bytes[6] = (bytes[6] & 0x0f) | 0x40
  bytes[8] = (bytes[8] & 0x3f) | 0x80

  const hex = [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('')

  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

export function useSession(): string {
  return getSessionId()
}
