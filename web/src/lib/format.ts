const stars = new Intl.NumberFormat('pl-PL')

export function formatStars(value: number): string {
  return value >= 10_000 ? `${(value / 1000).toFixed(value >= 100_000 ? 0 : 1)}k` : stars.format(value)
}

export function formatAgo(iso: string): string {
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000)

  if (days <= 0) return 'dzisiaj'
  if (days === 1) return 'wczoraj'
  if (days < 30) return `${days} dni temu`

  const months = Math.round(days / 30)

  return months < 12 ? `${months} mies. temu` : `${Math.round(months / 12)} lata temu`
}
