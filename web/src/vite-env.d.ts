/// <reference types="vite/client" />

interface ImportMetaEnv {
  // Puste w compose: nginx proxuje /api na kontener api, więc front i backend
  // siedzą na jednym originie. Ustawiane dopiero przy deployu na osobne domeny.
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
