import type { ApiError } from '../api/client'

export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center py-16 text-sm text-ink-400">
      {label}
    </div>
  )
}

export function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-md border border-dashed border-ink-700 bg-ink-900/40 p-8 text-center text-sm text-ink-400">
      {message}
    </div>
  )
}

export function ErrorBanner({ error }: { error: ApiError | null | undefined }) {
  if (!error) return null
  return (
    <div className="mb-4 rounded-md border border-rose-700/50 bg-rose-950/60 p-3 text-sm text-rose-200">
      <span className="font-medium">{error.status > 0 ? `Error ${error.status}` : 'Error'}:</span>{' '}
      {error.message}
    </div>
  )
}
