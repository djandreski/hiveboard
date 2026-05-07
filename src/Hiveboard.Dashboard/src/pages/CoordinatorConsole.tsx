import { useCallback, useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError, api, usePolling } from '../api/client'
import type {
  AgentSummary,
  ProjectResponse,
  TaskDetailResponse,
  TaskResponse,
} from '../api/types'
import { ErrorBanner, EmptyState, LoadingState } from '../components/PageState'
import { StatusBadge } from '../components/StatusBadge'
import { formatRelative } from '../lib/time'

interface ConsoleData {
  project: ProjectResponse
  tasks: TaskResponse[]
  details: Map<string, TaskDetailResponse>
  agents: AgentSummary[]
}

async function fetchConsole(projectId: string): Promise<ConsoleData> {
  const [project, tasks, agents] = await Promise.all([
    api.getProject(projectId),
    api.listTasks(projectId),
    api.listAgents(),
  ])

  // Pull detail for the actionable subset only — review + blocked tasks
  // need blockedReason and event context, plain backlog/assigned tasks don't.
  const actionable = tasks.filter(
    (task) => task.status === 'inreview' || task.status === 'blocked',
  )
  const detailEntries = await Promise.all(
    actionable.map(async (task) => {
      try {
        return [task.id, await api.getTask(task.id)] as const
      } catch {
        return null
      }
    }),
  )
  const details = new Map<string, TaskDetailResponse>()
  for (const entry of detailEntries) {
    if (entry) details.set(entry[0], entry[1])
  }

  return { project, tasks, details, agents }
}

export function CoordinatorConsole() {
  const { id } = useParams()
  const projectId = id ?? ''

  const fetcher = useCallback(() => fetchConsole(projectId), [projectId])
  const { data, error, loading, refetch } = usePolling(fetcher, [projectId])

  const inReview = data?.tasks.filter((task) => task.status === 'inreview') ?? []
  const blocked = data?.tasks.filter((task) => task.status === 'blocked') ?? []
  const assignmentGaps =
    data?.tasks.filter(
      (task) =>
        !task.assignedAgentId && task.status !== 'done' && task.status !== 'blocked',
    ) ?? []

  return (
    <div>
      <div className="mb-4">
        <Link to={`/projects/${projectId}/board`} className="text-xs text-ink-400 hover:text-ink-200">
          ← Back to board
        </Link>
        <h1 className="text-2xl font-semibold">Coordinator Console</h1>
        <p className="text-sm text-ink-400">
          Review approvals, blocker resolutions, and assignment gaps for this project.
        </p>
      </div>

      <ErrorBanner error={error} />

      {loading && !data ? <LoadingState /> : null}

      {data ? (
        <div className="grid gap-4 lg:grid-cols-2">
          <ConsoleSection
            title="Awaiting review"
            count={inReview.length}
            empty="No tasks waiting for review."
          >
            {inReview.map((task) => (
              <ReviewItem
                key={task.id}
                task={task}
                detail={data.details.get(task.id)}
                agents={data.agents}
                onChanged={refetch}
              />
            ))}
          </ConsoleSection>

          <ConsoleSection
            title="Blocked tasks"
            count={blocked.length}
            empty="No blocked tasks."
          >
            {blocked.map((task) => (
              <BlockedItem
                key={task.id}
                task={task}
                detail={data.details.get(task.id)}
                agents={data.agents}
                onChanged={refetch}
              />
            ))}
          </ConsoleSection>

          <ConsoleSection
            title="Assignment gaps"
            count={assignmentGaps.length}
            empty="Every active task has an assigned agent."
          >
            {assignmentGaps.map((task) => (
              <AssignmentGapItem
                key={task.id}
                task={task}
                agents={data.agents}
                onChanged={refetch}
              />
            ))}
          </ConsoleSection>
        </div>
      ) : null}
    </div>
  )
}

function ConsoleSection({
  title,
  count,
  empty,
  children,
}: {
  title: string
  count: number
  empty: string
  children: ReactNode
}) {
  return (
    <section className="card">
      <header className="mb-3 flex items-center justify-between">
        <h2 className="text-base font-semibold text-ink-100">{title}</h2>
        <span className="badge bg-ink-800 text-ink-200">{count}</span>
      </header>
      {count === 0 ? <EmptyState message={empty} /> : <div className="space-y-3">{children}</div>}
    </section>
  )
}

function ReviewItem({
  task,
  detail,
  agents,
  onChanged,
}: {
  task: TaskResponse
  detail: TaskDetailResponse | undefined
  agents: AgentSummary[]
  onChanged: () => Promise<void>
}) {
  const [busy, setBusy] = useState<'approve' | 'sendBack' | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const assignee = agents.find((agent) => agent.id === task.assignedAgentId)

  async function approve() {
    setBusy('approve')
    setActionError(null)
    try {
      await api.updateTaskStatus(task.id, { status: 'done' })
      await onChanged()
    } catch (err) {
      setActionError(messageOf(err))
    } finally {
      setBusy(null)
    }
  }

  async function sendBack() {
    setBusy('sendBack')
    setActionError(null)
    try {
      await api.updateTaskStatus(task.id, { status: 'inprogress' })
      await onChanged()
    } catch (err) {
      setActionError(messageOf(err))
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="rounded-md border border-ink-800 bg-ink-950/60 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={task.status} />
        <span className="font-medium text-ink-100">{task.title}</span>
        <span className="ml-auto text-xs text-ink-400">{formatRelative(task.updatedAt)}</span>
      </div>
      <div className="mt-1 text-xs text-ink-400">
        {assignee ? `Submitted by ${assignee.name}` : 'No assignee'}
      </div>
      {detail?.task.description ? (
        <p className="mt-2 line-clamp-3 text-sm text-ink-300">{detail.task.description}</p>
      ) : null}
      {actionError ? (
        <p className="mt-2 text-xs text-rose-300">{actionError}</p>
      ) : null}
      <div className="mt-3 flex gap-2">
        <button
          type="button"
          className="btn btn-primary"
          disabled={busy !== null}
          onClick={approve}
        >
          {busy === 'approve' ? 'Approving…' : 'Approve & mark done'}
        </button>
        <button
          type="button"
          className="btn"
          disabled={busy !== null}
          onClick={sendBack}
        >
          {busy === 'sendBack' ? 'Sending back…' : 'Send back to In Progress'}
        </button>
      </div>
    </div>
  )
}

function BlockedItem({
  task,
  detail,
  agents,
  onChanged,
}: {
  task: TaskResponse
  detail: TaskDetailResponse | undefined
  agents: AgentSummary[]
  onChanged: () => Promise<void>
}) {
  const [resolving, setResolving] = useState(false)
  const [reassignTarget, setReassignTarget] = useState<string>(task.assignedAgentId ?? '')
  const [actionError, setActionError] = useState<string | null>(null)

  async function unblock() {
    setResolving(true)
    setActionError(null)
    try {
      await api.updateTaskStatus(task.id, { status: 'inprogress' })
      await onChanged()
    } catch (err) {
      setActionError(messageOf(err))
    } finally {
      setResolving(false)
    }
  }

  async function reassignAndUnblock() {
    setResolving(true)
    setActionError(null)
    try {
      await api.updateTask(task.id, {
        assignedAgentId: reassignTarget || null,
      })
      await api.updateTaskStatus(task.id, { status: 'inprogress' })
      await onChanged()
    } catch (err) {
      setActionError(messageOf(err))
    } finally {
      setResolving(false)
    }
  }

  return (
    <div className="rounded-md border border-rose-700/40 bg-rose-950/30 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={task.status} />
        <span className="font-medium text-ink-100">{task.title}</span>
        <span className="ml-auto text-xs text-ink-400">{formatRelative(task.updatedAt)}</span>
      </div>
      <div className="mt-1 text-xs text-rose-200">
        {detail?.task.blockedReason ? `Reason: ${detail.task.blockedReason}` : 'No reason given'}
      </div>
      {actionError ? <p className="mt-2 text-xs text-rose-300">{actionError}</p> : null}

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <select
          className="input max-w-xs"
          value={reassignTarget}
          onChange={(event) => setReassignTarget(event.target.value)}
        >
          <option value="">— Unassigned —</option>
          {agents.map((agent) => (
            <option key={agent.id} value={agent.id}>
              {agent.name} ({agent.type})
            </option>
          ))}
        </select>
        <button
          type="button"
          className="btn btn-primary"
          disabled={resolving}
          onClick={unblock}
        >
          {resolving ? 'Working…' : 'Unblock (keep assignee)'}
        </button>
        <button
          type="button"
          className="btn"
          disabled={resolving || reassignTarget === (task.assignedAgentId ?? '')}
          onClick={reassignAndUnblock}
        >
          Reassign & unblock
        </button>
      </div>
    </div>
  )
}

function AssignmentGapItem({
  task,
  agents,
  onChanged,
}: {
  task: TaskResponse
  agents: AgentSummary[]
  onChanged: () => Promise<void>
}) {
  const [target, setTarget] = useState<string>('')
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  async function assign() {
    if (!target) return
    setBusy(true)
    setActionError(null)
    try {
      await api.updateTask(task.id, { assignedAgentId: target })
      await onChanged()
    } catch (err) {
      setActionError(messageOf(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="rounded-md border border-ink-800 bg-ink-950/60 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={task.status} />
        <span className="font-medium text-ink-100">{task.title}</span>
        <span className="ml-auto text-xs text-ink-400">{formatRelative(task.updatedAt)}</span>
      </div>
      {actionError ? <p className="mt-2 text-xs text-rose-300">{actionError}</p> : null}
      <div className="mt-3 flex flex-wrap items-center gap-2">
        <select
          className="input max-w-xs"
          value={target}
          onChange={(event) => setTarget(event.target.value)}
        >
          <option value="">— Pick an agent —</option>
          {agents.map((agent) => (
            <option key={agent.id} value={agent.id}>
              {agent.name} ({agent.type})
            </option>
          ))}
        </select>
        <button
          type="button"
          className="btn btn-primary"
          disabled={!target || busy}
          onClick={assign}
        >
          {busy ? 'Assigning…' : 'Assign'}
        </button>
      </div>
    </div>
  )
}

function messageOf(err: unknown): string {
  if (err instanceof ApiError) return err.message
  return (err as Error)?.message ?? 'Request failed'
}
