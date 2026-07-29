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

const chipBase = 'rounded-chip border px-3 py-1.5 text-sm transition'
const chipOn = 'border-ink bg-ink text-on-ink'
const chipOff = 'border-rule bg-panel text-muted hover:border-rule-strong hover:text-ink'

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
              className={`${chipBase} ${active ? chipOn : chipOff}`}
            >
              {language.name}
              <span className="ml-1.5 text-xs opacity-70">{source(language)}</span>
            </button>
          )
        })}

        {extra.map((name) => (
          <button key={name} type="button" onClick={() => toggle(name)} aria-pressed className={`${chipBase} ${chipOn}`}>
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
          aria-label="Dołóż język spoza profilu"
          className="w-48 rounded-chip border border-rule bg-panel px-3 py-1.5 text-sm text-ink outline-none transition placeholder:text-faint focus:border-rule-strong"
        />
        <button
          type="button"
          onClick={addCustom}
          className="rounded-chip border border-rule px-3 py-1.5 text-sm text-ink transition hover:border-rule-strong hover:bg-sunk"
        >
          Dodaj
        </button>
      </div>
    </div>
  )
}
