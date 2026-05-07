import { useCallback } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError, api, usePolling } from '../api/client'
import type { ProjectResponse, TaskContextEventResponse } from '../api/types'
import { ErrorBanner, EmptyState, LoadingState } from '../components/PageState'
import { formatAbsolute, formatRelative } from '../lib/time'

interface TimelineEvent extends TaskContextEventResponse {
  taskId: string
  taskTitle: string
}

interface TimelineData {
  project: ProjectResponse
  events: TimelineEvent[]
  partial: boolean
}

const MAX_TASKS_FETCHED = 40
const MAX_EVENTS_DISPLAYED = 200

async function fetchTimeline(projectId: string): Promise<TimelineData> {
  const [project, allTasks] = await Promise.all([
    api.getProject(projectId),
    api.listTasks(projectId),
  ])

  // Sort tasks by recent activity and cap fetched detail to keep this responsive
  // on larger projects. Older tasks fall outside the window.
  const sortedTasks = [...allTasks].sort(
    (a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime(),
  )
  const window = sortedTasks.slice(0, MAX_TASKS_FETCHED)

  const taskDetails = await Promise.all(
    window.map(async (task) => {
      try {
        return await api.getTask(task.id)
      } catch {
        return null
      }
    }),
  )

  const events: TimelineEvent[] = []
  for (const detail of taskDetails) {
    if (!detail) continue
    for (const event of detail.events) {
      events.push({ ...event, taskId: detail.task.id, taskTitle: detail.task.title })
    }
  }

  events.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())

  return {
    project,
    events: events.slice(0, MAX_EVENTS_DISPLAYED),
    partial: allTasks.length > window.length,
  }
}

export function EventTimeline() {
  const { id } = useParams()
  const projectId = id ?? ''

  const fetcher = useCallback(() => fetchTimeline(projectId), [projectId])
  const { data, error, loading } = usePolling(fetcher, [projectId])

  return (
    <div>
      <div className="mb-4">
        <Link to={`/projects/${projectId}/board`} className="text-xs text-ink-400 hover:text-ink-200">
          ← Back to board
        </Link>
        <h1 className="text-2xl font-semibold">{data?.project.name ?? 'Timeline'}</h1>
        <p className="text-sm text-ink-400">
          {data
            ? `${data.events.length} event${data.events.length === 1 ? '' : 's'}${
                data.partial ? ' (recent activity only)' : ''
              } · auto-refreshing every 10s`
            : 'Loading…'}
        </p>
      </div>

      <ErrorBanner error={error instanceof ApiError ? error : null} />

      {loading && !data ? <LoadingState /> : null}

      {data && data.events.length === 0 ? (
        <EmptyState message="No task events yet for this project." />
      ) : null}

      {data && data.events.length > 0 ? (
        <ol className="space-y-2">
          {data.events.map((event) => (
            <li key={event.id} className="card">
              <div className="flex flex-wrap items-baseline justify-between gap-2 text-xs text-ink-400">
                <span title={formatAbsolute(event.timestamp)}>
                  {formatRelative(event.timestamp)}
                </span>
                <span className="font-mono text-ink-500">{event.eventType}</span>
              </div>
              <div className="mt-1 text-sm text-ink-100">
                <span className="font-medium">{event.agent}</span>{' '}
                <span className="text-ink-300">on</span>{' '}
                <span className="font-medium">{event.taskTitle}</span>
              </div>
              {event.oldValue || event.newValue ? (
                <div className="mt-1 font-mono text-xs text-ink-300">
                  {event.oldValue ?? '∅'} → {event.newValue ?? '∅'}
                </div>
              ) : null}
            </li>
          ))}
        </ol>
      ) : null}
    </div>
  )
}
