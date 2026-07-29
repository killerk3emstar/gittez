// Klasy powtarzalnych elementów w jednym miejscu - inaczej ten sam przycisk
// dostaje w piątym pliku inny padding i całość zaczyna wyglądać na złożoną
// z przypadków.

export const panel = 'rounded-panel border border-rule bg-panel'

// Stan wyłączony to jasny kafel, nie przygaszony czarny prostokąt: ten drugi
// na pierwszym wejściu czyta się jak zepsuty przycisk, a nie jak czekający.
export const buttonPrimary =
  'inline-flex items-center justify-center rounded-chip bg-ink px-4 py-2.5 text-sm font-medium text-on-ink transition hover:bg-ink-soft disabled:cursor-not-allowed disabled:bg-sunk disabled:text-faint'

export const buttonGhost =
  'inline-flex items-center justify-center rounded-chip border border-rule px-3 py-1.5 text-sm text-ink transition hover:border-rule-strong hover:bg-sunk'

export const field =
  'w-full rounded-chip border border-rule bg-panel px-3 py-2.5 text-sm text-ink outline-none transition placeholder:text-faint focus:border-rule-strong'
