export function formatRelative(input: string | Date | null | undefined): string {
  if (!input) return '—'
  const date = typeof input === 'string' ? new Date(input) : input
  const ms = Date.now() - date.getTime()
  if (Number.isNaN(ms)) return '—'

  const abs = Math.abs(ms)
  const seconds = Math.round(abs / 1000)
  if (seconds < 5) return 'just now'
  if (seconds < 60) return `${seconds}s ago`
  const minutes = Math.round(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.round(hours / 24)
  if (days < 30) return `${days}d ago`
  const months = Math.round(days / 30)
  if (months < 12) return `${months}mo ago`
  const years = Math.round(days / 365)
  return `${years}y ago`
}

export function formatAbsolute(input: string | Date | null | undefined): string {
  if (!input) return '—'
  const date = typeof input === 'string' ? new Date(input) : input
  if (Number.isNaN(date.getTime())) return '—'
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function isOnline(lastSeenAt: string | null | undefined, thresholdMinutes = 5): boolean {
  if (!lastSeenAt) return false
  const ms = Date.now() - new Date(lastSeenAt).getTime()
  if (Number.isNaN(ms)) return false
  return ms < thresholdMinutes * 60 * 1000
}
