const stars = new Intl.NumberFormat('pl-PL')

export function formatStars(value: number): string {
  return value >= 10_000 ? `${(value / 1000).toFixed(value >= 100_000 ? 0 : 1)}k` : stars.format(value)
}

// Polski ma trzy formy: 1 komentarz, 2-4 komentarze, 5+ komentarzy - z
// wyjątkiem nastek, gdzie 12-14 wraca do formy mnogiej.
export function formatComments(count: number): string {
  if (count === 1) return '1 komentarz'

  const last = count % 10
  const lastTwo = count % 100
  const few = last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)

  return `${count} ${few ? 'komentarze' : 'komentarzy'}`
}

export function formatAgo(iso: string): string {
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000)

  if (days <= 0) return 'dzisiaj'
  if (days === 1) return 'wczoraj'
  if (days < 30) return `${days} dni temu`

  const months = Math.round(days / 30)
  if (months < 12) return `${months} mies. temu`

  const years = Math.round(months / 12)

  // "1 lata temu" i "5 lata temu" to dwa różne błędy - polski ma trzy formy.
  if (years === 1) return 'rok temu'

  return years < 5 ? `${years} lata temu` : `${years} lat temu`
}
