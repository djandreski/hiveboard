import { useCallback, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { ApiError, api, usePolling } from '../api/client'
import type { AgentSummary, ProjectResponse, TaskResponse } from '../api/types'
import { TASK_STATUSES, TASK_STATUS_LABELS } from '../api/types'
import { Modal } from '../components/Modal'
import { ErrorBanner, LoadingState, EmptyState } from '../components/PageState'
import { isOnline } from '../lib/time'

interface ProjectStats {
  project: ProjectResponse
  tasks: TaskResponse[] | null
  loadError: string | null
}

interface OverviewData {
  projects: ProjectResponse[]
  stats: ProjectStats[]
  agents: AgentSummary[]
}

async function fetchOverview(): Promise<OverviewData> {
  const [projects, agents] = await Promise.all([api.listProjects(), api.listAgents()])
  const stats = await Promise.all(
    projects.map(async (project) => {
      try {
        const tasks = await api.listTasks(project.id)
        return { project, tasks, loadError: null }
      } catch (err) {
        const message =
          err instanceof ApiError ? err.message : (err as Error)?.message ?? 'Failed to load tasks'
        return { project, tasks: null, loadError: message }
      }
    }),
  )
  return { projects, stats, agents }
}

export function ProjectOverview() {
  const overview = usePolling(fetchOverview, [])
  const [creating, setCreating] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const refetch = overview.refetch

  const handleCreate = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault()
      const trimmed = name.trim()
      if (!trimmed) return
      setSubmitting(true)
      setSubmitError(null)
      try {
        await api.createProject({
          name: trimmed,
          description: description.trim() || undefined,
        })
        setName('')
        setDescription('')
        setCreating(false)
        await refetch()
      } catch (err) {
        setSubmitError(
          err instanceof ApiError
            ? err.message
            : (err as Error)?.message ?? 'Could not create project',
        )
      } finally {
        setSubmitting(false)
      }
    },
    [name, description, refetch],
  )

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Projects</h1>
          <p className="text-sm text-ink-400">
            {overview.data
              ? `${overview.data.projects.length} project${overview.data.projects.length === 1 ? '' : 's'} · auto-refreshing every 10s`
              : 'Loading projects…'}
          </p>
        </div>
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => setCreating(true)}
        >
          New project
        </button>
      </div>

      <ErrorBanner error={overview.error} />

      {overview.loading && !overview.data ? <LoadingState /> : null}

      {overview.data && overview.data.projects.length === 0 ? (
        <EmptyState message="No projects yet. Create your first one to get started." />
      ) : null}

      {overview.data ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {overview.data.stats.map((stat) => (
            <ProjectCard key={stat.project.id} stat={stat} agents={overview.data!.agents} />
          ))}
        </div>
      ) : null}

      <Modal
        open={creating}
        onClose={() => {
          if (!submitting) {
            setCreating(false)
            setSubmitError(null)
          }
        }}
        title="Create project"
        footer={
          <>
            <button
              type="button"
              className="btn"
              onClick={() => setCreating(false)}
              disabled={submitting}
            >
              Cancel
            </button>
            <button
              type="submit"
              form="create-project-form"
              className="btn btn-primary"
              disabled={submitting || !name.trim()}
            >
              {submitting ? 'Creating…' : 'Create'}
            </button>
          </>
        }
      >
        <form id="create-project-form" onSubmit={handleCreate} className="space-y-3">
          <label className="block">
            <span className="label">Name</span>
            <input
              autoFocus
              required
              maxLength={120}
              value={name}
              onChange={(event) => setName(event.target.value)}
              className="input mt-1"
              placeholder="e.g. Internal CMS"
            />
          </label>
          <label className="block">
            <span className="label">Description (optional)</span>
            <textarea
              rows={3}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              className="input mt-1"
            />
          </label>
          {submitError ? (
            <div className="rounded-md border border-rose-700/50 bg-rose-950/60 p-2 text-sm text-rose-200">
              {submitError}
            </div>
          ) : null}
        </form>
      </Modal>
    </div>
  )
}

function ProjectCard({ stat, agents }: { stat: ProjectStats; agents: AgentSummary[] }) {
  const { project, tasks, loadError } = stat
  const counts = countByStatus(tasks ?? [])
  const total = tasks?.length ?? 0
  const completed = counts.done ?? 0
  const completion = total === 0 ? 0 : Math.round((completed / total) * 100)
  const activeAgentIds = new Set(
    (tasks ?? [])
      .filter((task) => task.status !== 'done' && task.assignedAgentId)
      .map((task) => task.assignedAgentId!),
  )
  const onlineAgents = agents.filter(
    (agent) => activeAgentIds.has(agent.id) && isOnline(agent.lastSeenAt),
  )

  return (
    <Link
      to={`/projects/${project.id}/board`}
      className="card block transition hover:border-accent/60 hover:shadow-md"
    >
      <div className="flex items-start justify-between gap-2">
        <div>
          <h2 className="text-base font-semibold text-ink-100">{project.name}</h2>
          {project.description ? (
            <p className="mt-1 line-clamp-2 text-xs text-ink-400">{project.description}</p>
          ) : null}
        </div>
        <span className="badge bg-ink-800 text-ink-200">{project.status}</span>
      </div>

      {loadError ? (
        <div className="mt-3 text-xs text-rose-300">Could not load tasks: {loadError}</div>
      ) : (
        <>
          <div className="mt-3 flex items-baseline gap-2">
            <span className="text-2xl font-semibold text-ink-100">{completion}%</span>
            <span className="text-xs text-ink-400">
              {completed}/{total} task{total === 1 ? '' : 's'} done
            </span>
          </div>

          <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-ink-800">
            <div
              className="h-full bg-emerald-500 transition-[width]"
              style={{ width: `${completion}%` }}
            />
          </div>

          <div className="mt-4 flex flex-wrap gap-2 text-[11px]">
            {TASK_STATUSES.map((status) => (
              <span key={status} className="rounded-md bg-ink-800 px-2 py-0.5 text-ink-300">
                {TASK_STATUS_LABELS[status]}: {counts[status] ?? 0}
              </span>
            ))}
          </div>

          <div className="mt-3 text-xs text-ink-400">
            {onlineAgents.length} agent{onlineAgents.length === 1 ? '' : 's'} online ·{' '}
            {activeAgentIds.size} assigned
          </div>
        </>
      )}
    </Link>
  )
}

function countByStatus(tasks: TaskResponse[]): Partial<Record<string, number>> {
  return tasks.reduce<Partial<Record<string, number>>>((acc, task) => {
    acc[task.status] = (acc[task.status] ?? 0) + 1
    return acc
  }, {})
}
