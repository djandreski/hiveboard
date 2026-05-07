import {
  useCallback,
  useMemo,
  useState,
  type FormEvent,
} from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError, api, usePolling } from '../api/client'
import type {
  AgentSummary,
  EpicResponse,
  ProjectResponse,
  TaskDetailResponse,
  TaskResponse,
  TaskStatus,
} from '../api/types'
import { TASK_STATUSES, TASK_STATUS_LABELS } from '../api/types'
import { Modal } from '../components/Modal'
import { ErrorBanner, LoadingState } from '../components/PageState'
import { StatusBadge } from '../components/StatusBadge'
import { formatRelative } from '../lib/time'

interface BoardData {
  project: ProjectResponse
  tasks: TaskResponse[]
  agents: AgentSummary[]
  epics: EpicResponse[]
}

const COLUMN_ORDER: TaskStatus[] = [
  'backlog',
  'assigned',
  'inprogress',
  'inreview',
  'done',
  'blocked',
]

export function TaskBoard() {
  const { id } = useParams()
  const projectId = id ?? ''

  const fetcher = useCallback(async (): Promise<BoardData> => {
    const [project, tasks, agents, epics] = await Promise.all([
      api.getProject(projectId),
      api.listTasks(projectId),
      api.listAgents(),
      api.listEpics(projectId),
    ])
    return { project, tasks, agents, epics }
  }, [projectId])

  const { data, error, loading, refetch } = usePolling(fetcher, [projectId])

  const [creating, setCreating] = useState(false)
  const [editingTaskId, setEditingTaskId] = useState<string | null>(null)

  const tasksByColumn = useMemo(() => {
    const groups: Record<TaskStatus, TaskResponse[]> = {
      backlog: [],
      assigned: [],
      inprogress: [],
      inreview: [],
      done: [],
      blocked: [],
    }
    for (const task of data?.tasks ?? []) {
      const bucket = groups[task.status] ?? groups.backlog
      bucket.push(task)
    }
    return groups
  }, [data?.tasks])

  const agentsById = useMemo(() => {
    const map = new Map<string, AgentSummary>()
    for (const agent of data?.agents ?? []) map.set(agent.id, agent)
    return map
  }, [data?.agents])

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <div>
          <Link to="/" className="text-xs text-ink-400 hover:text-ink-200">
            ← All projects
          </Link>
          <h1 className="text-2xl font-semibold">{data?.project.name ?? 'Task Board'}</h1>
          <p className="text-sm text-ink-400">
            {data ? `${data.tasks.length} tasks · auto-refreshing every 10s` : 'Loading…'}
          </p>
        </div>
        <button type="button" className="btn btn-primary" onClick={() => setCreating(true)}>
          New task
        </button>
      </div>

      <ErrorBanner error={error} />

      {loading && !data ? (
        <LoadingState />
      ) : (
        <div className="grid grid-flow-col auto-cols-[minmax(260px,1fr)] gap-3 overflow-x-auto pb-3">
          {COLUMN_ORDER.map((status) => (
            <BoardColumn
              key={status}
              status={status}
              tasks={tasksByColumn[status] ?? []}
              agentsById={agentsById}
              onCardClick={(taskId) => setEditingTaskId(taskId)}
            />
          ))}
        </div>
      )}

      {data ? (
        <CreateTaskModal
          open={creating}
          onClose={() => setCreating(false)}
          projectId={projectId}
          epics={data.epics}
          onCreated={refetch}
        />
      ) : null}

      {editingTaskId ? (
        <TaskDetailModal
          taskId={editingTaskId}
          agents={data?.agents ?? []}
          onClose={() => setEditingTaskId(null)}
          onChanged={refetch}
        />
      ) : null}
    </div>
  )
}

function BoardColumn({
  status,
  tasks,
  agentsById,
  onCardClick,
}: {
  status: TaskStatus
  tasks: TaskResponse[]
  agentsById: Map<string, AgentSummary>
  onCardClick: (taskId: string) => void
}) {
  const isBlocked = status === 'blocked'
  return (
    <div
      className={`flex flex-col rounded-lg border border-ink-800 bg-ink-900/50 ${
        isBlocked ? 'border-rose-700/40' : ''
      }`}
    >
      <div
        className={`flex items-center justify-between border-b px-3 py-2 ${
          isBlocked ? 'border-rose-700/40 text-rose-200' : 'border-ink-800 text-ink-200'
        }`}
      >
        <span className="text-sm font-semibold">{TASK_STATUS_LABELS[status]}</span>
        <span className="text-xs text-ink-400">{tasks.length}</span>
      </div>
      <div className="flex flex-1 flex-col gap-2 p-2">
        {tasks.length === 0 ? (
          <p className="px-2 py-3 text-center text-xs text-ink-500">No tasks</p>
        ) : (
          tasks.map((task) => (
            <TaskCard
              key={task.id}
              task={task}
              agent={task.assignedAgentId ? agentsById.get(task.assignedAgentId) : undefined}
              isBlockedColumn={isBlocked}
              onClick={() => onCardClick(task.id)}
            />
          ))
        )}
      </div>
    </div>
  )
}

function TaskCard({
  task,
  agent,
  isBlockedColumn,
  onClick,
}: {
  task: TaskResponse
  agent: AgentSummary | undefined
  isBlockedColumn: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-md border bg-ink-950 p-3 text-left transition hover:border-accent/60 ${
        isBlockedColumn ? 'border-rose-700/60 ring-1 ring-rose-700/30' : 'border-ink-700'
      }`}
    >
      <h3 className="text-sm font-medium text-ink-100">{task.title}</h3>
      <div className="mt-2 flex items-center justify-between text-xs text-ink-400">
        <span>{agent ? agent.name : 'Unassigned'}</span>
        <span>{formatRelative(task.updatedAt)}</span>
      </div>
    </button>
  )
}

function CreateTaskModal({
  open,
  onClose,
  projectId,
  epics,
  onCreated,
}: {
  open: boolean
  onClose: () => void
  projectId: string
  epics: EpicResponse[]
  onCreated: () => Promise<void>
}) {
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [epicId, setEpicId] = useState<string>('')
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  function reset() {
    setTitle('')
    setDescription('')
    setEpicId('')
    setSubmitError(null)
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!title.trim()) return
    setSubmitting(true)
    setSubmitError(null)
    try {
      await api.createTask(projectId, {
        title: title.trim(),
        description: description.trim() || undefined,
        epicId: epicId || undefined,
      })
      reset()
      onClose()
      await onCreated()
    } catch (err) {
      setSubmitError(
        err instanceof ApiError ? err.message : (err as Error)?.message ?? 'Could not create task',
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal
      open={open}
      onClose={() => {
        if (!submitting) {
          reset()
          onClose()
        }
      }}
      title="Create task"
      footer={
        <>
          <button type="button" className="btn" onClick={onClose} disabled={submitting}>
            Cancel
          </button>
          <button
            type="submit"
            form="create-task-form"
            className="btn btn-primary"
            disabled={submitting || !title.trim()}
          >
            {submitting ? 'Creating…' : 'Create'}
          </button>
        </>
      }
    >
      <form id="create-task-form" onSubmit={handleSubmit} className="space-y-3">
        <label className="block">
          <span className="label">Title</span>
          <input
            autoFocus
            required
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            className="input mt-1"
          />
        </label>
        <label className="block">
          <span className="label">Description</span>
          <textarea
            rows={4}
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            className="input mt-1"
          />
        </label>
        {epics.length > 0 ? (
          <label className="block">
            <span className="label">Epic (optional)</span>
            <select
              value={epicId}
              onChange={(event) => setEpicId(event.target.value)}
              className="input mt-1"
            >
              <option value="">— None —</option>
              {epics.map((epic) => (
                <option key={epic.id} value={epic.id}>
                  {epic.title}
                </option>
              ))}
            </select>
          </label>
        ) : null}
        {submitError ? (
          <div className="rounded-md border border-rose-700/50 bg-rose-950/60 p-2 text-sm text-rose-200">
            {submitError}
          </div>
        ) : null}
      </form>
    </Modal>
  )
}

function TaskDetailModal({
  taskId,
  agents,
  onClose,
  onChanged,
}: {
  taskId: string
  agents: AgentSummary[]
  onClose: () => void
  onChanged: () => Promise<void>
}) {
  const fetcher = useCallback(() => api.getTask(taskId), [taskId])
  const { data, error, loading, refetch } = usePolling(fetcher, [taskId])

  const [editingMeta, setEditingMeta] = useState(false)
  const [draftTitle, setDraftTitle] = useState('')
  const [draftDescription, setDraftDescription] = useState('')
  const [draftAssignee, setDraftAssignee] = useState<string>('')
  const [statusBusy, setStatusBusy] = useState<TaskStatus | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [blockedReason, setBlockedReason] = useState('')

  function startEdit() {
    if (!data) return
    setDraftTitle(data.task.title)
    setDraftDescription(data.task.description)
    setDraftAssignee(data.task.assignedAgentId ?? '')
    setEditingMeta(true)
    setActionError(null)
  }

  async function saveMeta(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!data) return
    setActionError(null)
    try {
      await api.updateTask(taskId, {
        title: draftTitle.trim() || undefined,
        description: draftDescription,
        assignedAgentId: draftAssignee ? draftAssignee : null,
      })
      setEditingMeta(false)
      await Promise.all([refetch(), onChanged()])
    } catch (err) {
      setActionError(
        err instanceof ApiError ? err.message : (err as Error)?.message ?? 'Update failed',
      )
    }
  }

  async function moveToStatus(status: TaskStatus, extra?: { blockedReason?: string }) {
    setActionError(null)
    setStatusBusy(status)
    try {
      await api.updateTaskStatus(taskId, {
        status,
        blockedReason: extra?.blockedReason,
      })
      setBlockedReason('')
      await Promise.all([refetch(), onChanged()])
    } catch (err) {
      setActionError(
        err instanceof ApiError ? err.message : (err as Error)?.message ?? 'Status update failed',
      )
    } finally {
      setStatusBusy(null)
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={data?.task.title ?? 'Task'}
      size="lg"
      footer={
        <button type="button" className="btn" onClick={onClose}>
          Close
        </button>
      }
    >
      <ErrorBanner error={error} />
      {actionError ? (
        <div className="mb-3 rounded-md border border-rose-700/50 bg-rose-950/60 p-2 text-sm text-rose-200">
          {actionError}
        </div>
      ) : null}

      {loading && !data ? <LoadingState /> : null}

      {data ? (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={data.task.status} />
            {data.task.blockedReason ? (
              <span className="text-xs text-rose-300">Blocked: {data.task.blockedReason}</span>
            ) : null}
            <span className="ml-auto text-xs text-ink-400">
              Updated {formatRelative(data.task.updatedAt)}
            </span>
          </div>

          {editingMeta ? (
            <form onSubmit={saveMeta} className="space-y-3">
              <label className="block">
                <span className="label">Title</span>
                <input
                  required
                  value={draftTitle}
                  onChange={(event) => setDraftTitle(event.target.value)}
                  className="input mt-1"
                />
              </label>
              <label className="block">
                <span className="label">Description</span>
                <textarea
                  rows={5}
                  value={draftDescription}
                  onChange={(event) => setDraftDescription(event.target.value)}
                  className="input mt-1"
                />
              </label>
              <label className="block">
                <span className="label">Assigned agent</span>
                <select
                  value={draftAssignee}
                  onChange={(event) => setDraftAssignee(event.target.value)}
                  className="input mt-1"
                >
                  <option value="">— Unassigned —</option>
                  {agents.map((agent) => (
                    <option key={agent.id} value={agent.id}>
                      {agent.name} ({agent.type})
                    </option>
                  ))}
                </select>
              </label>
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  className="btn"
                  onClick={() => setEditingMeta(false)}
                >
                  Cancel
                </button>
                <button type="submit" className="btn btn-primary">
                  Save
                </button>
              </div>
            </form>
          ) : (
            <div className="space-y-3 text-sm text-ink-200">
              {data.task.description ? (
                <p className="whitespace-pre-wrap">{data.task.description}</p>
              ) : (
                <p className="italic text-ink-500">No description.</p>
              )}
              <p className="text-xs text-ink-400">
                Assigned to:{' '}
                {data.task.assignedAgentId
                  ? agents.find((agent) => agent.id === data.task.assignedAgentId)?.name ??
                    'Unknown agent'
                  : 'Unassigned'}
              </p>
              <button type="button" className="btn" onClick={startEdit}>
                Edit / Reassign
              </button>
            </div>
          )}

          <div>
            <span className="label">Move to</span>
            <div className="mt-2 flex flex-wrap gap-2">
              {TASK_STATUSES.filter((status) => status !== 'blocked').map((status) => (
                <button
                  key={status}
                  type="button"
                  className="btn"
                  disabled={status === data.task.status || statusBusy !== null}
                  onClick={() => moveToStatus(status)}
                >
                  {statusBusy === status ? '…' : TASK_STATUS_LABELS[status]}
                </button>
              ))}
            </div>
          </div>

          {data.task.status !== 'blocked' ? (
            <div className="rounded-md border border-rose-700/30 bg-rose-950/30 p-3">
              <span className="label text-rose-300">Mark blocked</span>
              <div className="mt-2 flex gap-2">
                <input
                  className="input flex-1"
                  placeholder="Why is this blocked?"
                  value={blockedReason}
                  onChange={(event) => setBlockedReason(event.target.value)}
                />
                <button
                  type="button"
                  className="btn btn-danger"
                  disabled={!blockedReason.trim() || statusBusy !== null}
                  onClick={() => moveToStatus('blocked', { blockedReason: blockedReason.trim() })}
                >
                  Block
                </button>
              </div>
            </div>
          ) : null}

          {data.events.length > 0 ? (
            <details className="rounded-md border border-ink-800 bg-ink-950/40 p-3">
              <summary className="cursor-pointer text-sm text-ink-300">
                Recent events ({data.events.length})
              </summary>
              <ul className="mt-2 space-y-2 text-xs">
                {data.events.slice(0, 20).map((event) => (
                  <EventRow key={event.id} event={event} />
                ))}
              </ul>
            </details>
          ) : null}

          {data.notes.length > 0 ? (
            <details className="rounded-md border border-ink-800 bg-ink-950/40 p-3">
              <summary className="cursor-pointer text-sm text-ink-300">
                Notes ({data.notes.length})
              </summary>
              <ul className="mt-2 space-y-2 text-xs">
                {data.notes.map((note, idx) => (
                  <li key={idx} className="border-l border-ink-700 pl-2 text-ink-200">
                    <span className="font-medium">{note.agent}</span> ·{' '}
                    <span className="text-ink-500">{note.type}</span>
                    <p className="whitespace-pre-wrap">{note.content}</p>
                  </li>
                ))}
              </ul>
            </details>
          ) : null}
        </div>
      ) : null}
    </Modal>
  )
}

function EventRow({ event }: { event: TaskDetailResponse['events'][number] }) {
  const change =
    event.oldValue || event.newValue
      ? `${event.oldValue ?? '∅'} → ${event.newValue ?? '∅'}`
      : null
  return (
    <li className="text-ink-300">
      <span className="text-ink-500">{formatRelative(event.timestamp)}</span> ·{' '}
      <span className="font-medium text-ink-200">{event.agent}</span>{' '}
      <span className="text-ink-300">{event.eventType}</span>
      {change ? <span className="ml-1 text-ink-400">({change})</span> : null}
    </li>
  )
}
