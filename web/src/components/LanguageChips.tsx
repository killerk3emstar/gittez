import { useState } from 'react'
import type { ProfileLanguage } from '../api/types'

type Props = {
  detected: ProfileLanguage[]
  selected: string[]
  onChange: (languages: string[]) => void
}

// Etykieta źródła jest tu po to, żeby recenzent zobaczył, skąd wzięliśmy język:
// kontrybucja do cudzego projektu waży tyle co własne repo (SPEC §9).
function source(language: ProfileLanguage): string {
  const parts: string[] = []

  if (language.ownedRepos > 0) parts.push(`${language.ownedRepos} repo`)
  if (language.contributedRepos > 0) parts.push(`${language.contributedRepos} kontrybucje`)

  return parts.join(', ')
}

export function LanguageChips({ detected, selected, onChange }: Props) {
  const [custom, setCustom] = useState('')

  const selectedLower = new Set(selected.map((l) => l.toLowerCase()))
  const extra = selected.filter((l) => !detected.some((d) => d.name.toLowerCase() === l.toLowerCase()))

  const toggle = (name: string) => {
    onChange(
      selectedLower.has(name.toLowerCase())
        ? selected.filter((l) => l.toLowerCase() !== name.toLowerCase())
        : [...selected, name],
    )
  }

  const addCustom = () => {
    const value = custom.trim()
    if (value.length === 0 || selectedLower.has(value.toLowerCase())) {
      setCustom('')
      return
    }

    onChange([...selected, value])
    setCustom('')
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2">
        {detected.map((language) => {
          const active = selectedLower.has(language.name.toLowerCase())

          return (
            <button
              key={language.name}
              type="button"
              onClick={() => toggle(language.name)}
              aria-pressed={active}
              className={`rounded-full border px-3 py-1.5 text-sm transition ${
                active
                  ? 'border-sky-400/60 bg-sky-500/15 text-sky-200'
                  : 'border-ink-700 bg-transparent text-ink-400 hover:border-ink-400 hover:text-ink-200'
              }`}
            >
              {language.name}
              <span className="ml-1.5 text-xs opacity-70">{source(language)}</span>
            </button>
          )
        })}

        {extra.map((name) => (
          <button
            key={name}
            type="button"
            onClick={() => toggle(name)}
            aria-pressed
            className="rounded-full border border-sky-400/60 bg-sky-500/15 px-3 py-1.5 text-sm text-sky-200 transition"
          >
            {name}
            <span className="ml-1.5 text-xs opacity-70">dodany ręcznie</span>
          </button>
        ))}
      </div>

      <div className="flex gap-2">
        <input
          value={custom}
          onChange={(e) => setCustom(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              addCustom()
            }
          }}
          placeholder="Dołóż język, np. Rust"
          className="w-48 rounded-lg border border-ink-700 bg-ink-900 px-3 py-1.5 text-sm text-ink-200 outline-none placeholder:text-ink-700 focus:border-sky-400/60"
        />
        <button
          type="button"
          onClick={addCustom}
          className="rounded-lg border border-ink-700 px-3 py-1.5 text-sm text-ink-200 transition hover:bg-ink-800"
        >
          Dodaj
        </button>
      </div>
    </div>
  )
}
