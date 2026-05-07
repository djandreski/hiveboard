import { useMemo } from 'react'
import { api, usePolling } from '../api/client'
import type { AgentSummary, ProjectResponse, TaskResponse } from '../api/types'
import { ErrorBanner, EmptyState, LoadingState } from '../components/PageState'
import { GenericBadge, StatusBadge } from '../components/StatusBadge'
import { formatAbsolute, formatRelative, isOnline } from '../lib/time'

interface AgentActivityData {
  agents: AgentSummary[]
  tasksByAgent: Map<string, { task: TaskResponse; project: ProjectResponse }>
}

async function fetchActivity(): Promise<AgentActivityData> {
  const [agents, projects] = await Promise.all([api.listAgents(), api.listProjects()])
  // Pull active tasks per project so we can show "current task" per agent.
  const projectTaskLists = await Promise.all(
    projects.map(async (project) => ({
      project,
      tasks: await api.listTasks(project.id),
    })),
  )

  const tasksByAgent = new Map<string, { task: TaskResponse; project: ProjectResponse }>()
  for (const { project, tasks } of projectTaskLists) {
    for (const task of tasks) {
      if (!task.assignedAgentId) continue
      if (task.status === 'done') continue
      const current = tasksByAgent.get(task.assignedAgentId)
      // Prefer the most recently updated active task as "current".
      if (
        !current ||
        new Date(task.updatedAt).getTime() > new Date(current.task.updatedAt).getTime()
      ) {
        tasksByAgent.set(task.assignedAgentId, { task, project })
      }
    }
  }

  return { agents, tasksByAgent }
}

export function AgentActivity() {
  const { data, error, loading } = usePolling(fetchActivity, [])

  const sortedAgents = useMemo(() => {
    if (!data) return []
    return [...data.agents].sort((a, b) => {
      const aOnline = isOnline(a.lastSeenAt) ? 0 : 1
      const bOnline = isOnline(b.lastSeenAt) ? 0 : 1
      if (aOnline !== bOnline) return aOnline - bOnline
      const aTime = a.lastSeenAt ? new Date(a.lastSeenAt).getTime() : 0
      const bTime = b.lastSeenAt ? new Date(b.lastSeenAt).getTime() : 0
      return bTime - aTime
    })
  }, [data])

  return (
    <div>
      <div className="mb-4">
        <h1 className="text-2xl font-semibold">Agents</h1>
        <p className="text-sm text-ink-400">
          {data
            ? `${data.agents.length} agent${data.agents.length === 1 ? '' : 's'} · auto-refreshing every 10s`
            : 'Loading…'}
        </p>
      </div>

      <ErrorBanner error={error} />

      {loading && !data ? <LoadingState /> : null}

      {data && data.agents.length === 0 ? (
        <EmptyState message="No agents registered yet. Use POST /api/v1/agents/register to register agents." />
      ) : null}

      {data && data.agents.length > 0 ? (
        <div className="overflow-hidden rounded-lg border border-ink-800">
          <table className="w-full divide-y divide-ink-800 text-sm">
            <thead className="bg-ink-900 text-ink-300">
              <tr>
                <th className="px-3 py-2 text-left font-medium">Status</th>
                <th className="px-3 py-2 text-left font-medium">Name</th>
                <th className="px-3 py-2 text-left font-medium">Type</th>
                <th className="px-3 py-2 text-left font-medium">Platform</th>
                <th className="px-3 py-2 text-left font-medium">Current task</th>
                <th className="px-3 py-2 text-left font-medium">Last seen</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-ink-800 bg-ink-950">
              {sortedAgents.map((agent) => {
                const online = isOnline(agent.lastSeenAt)
                const inactive = agent.status === 'inactive'
                const current = data.tasksByAgent.get(agent.id)
                return (
                  <tr key={agent.id} className="hover:bg-ink-900/50">
                    <td className="px-3 py-2">
                      {inactive ? (
                        <GenericBadge label="Inactive" tone="neutral" />
                      ) : online ? (
                        <GenericBadge label="● Online" tone="positive" />
                      ) : (
                        <GenericBadge label="○ Offline" tone="neutral" />
                      )}
                    </td>
                    <td className="px-3 py-2 font-medium text-ink-100">{agent.name}</td>
                    <td className="px-3 py-2 capitalize text-ink-300">{agent.type}</td>
                    <td className="px-3 py-2 capitalize text-ink-300">{agent.platform}</td>
                    <td className="px-3 py-2">
                      {current ? (
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <StatusBadge status={current.task.status} />
                            <span className="truncate text-ink-100">{current.task.title}</span>
                          </div>
                          <div className="text-xs text-ink-500">
                            {current.project.name}
                          </div>
                        </div>
                      ) : (
                        <span className="text-ink-500">Idle</span>
                      )}
                    </td>
                    <td className="px-3 py-2 text-ink-300" title={formatAbsolute(agent.lastSeenAt)}>
                      {formatRelative(agent.lastSeenAt)}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  )
}
