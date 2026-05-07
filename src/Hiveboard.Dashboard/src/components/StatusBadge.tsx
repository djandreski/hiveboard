import type { TaskStatus } from '../api/types'
import { TASK_STATUS_LABELS } from '../api/types'

const statusClasses: Record<TaskStatus, string> = {
  backlog: 'bg-ink-700 text-ink-100',
  assigned: 'bg-blue-900/60 text-blue-200',
  inprogress: 'bg-amber-900/60 text-amber-200',
  inreview: 'bg-purple-900/60 text-purple-200',
  done: 'bg-emerald-900/60 text-emerald-200',
  blocked: 'bg-rose-900/70 text-rose-200',
}

export function StatusBadge({ status }: { status: TaskStatus }) {
  return (
    <span className={`badge ${statusClasses[status] ?? 'bg-ink-700 text-ink-100'}`}>
      {TASK_STATUS_LABELS[status] ?? status}
    </span>
  )
}

export function GenericBadge({
  label,
  tone = 'neutral',
}: {
  label: string
  tone?: 'neutral' | 'positive' | 'warn' | 'danger' | 'info'
}) {
  const toneClass = {
    neutral: 'bg-ink-700 text-ink-100',
    positive: 'bg-emerald-900/60 text-emerald-200',
    warn: 'bg-amber-900/60 text-amber-200',
    danger: 'bg-rose-900/60 text-rose-200',
    info: 'bg-blue-900/60 text-blue-200',
  }[tone]
  return <span className={`badge ${toneClass}`}>{label}</span>
}
