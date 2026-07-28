const storageKey = 'gitbounty.sessionId'

// Anonimowy UUID, nie uwierzytelnienie: da się go podrobić, ale nie chroni
// niczego wrażliwego (SPEC §7). Wiersz w bazie powstaje przy pierwszym zapisie.
export function getSessionId(): string {
  const stored = localStorage.getItem(storageKey)
  if (stored) return stored

  const created = crypto.randomUUID()
  localStorage.setItem(storageKey, created)

  return created
}

export function useSession(): string {
  return getSessionId()
}
