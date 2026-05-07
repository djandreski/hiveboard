// Mirrors of the Hiveboard.Api.Contracts records.
// Property names are camelCased per ASP.NET Core's default JSON policy.

export type TaskStatus =
  | 'backlog'
  | 'assigned'
  | 'inprogress'
  | 'inreview'
  | 'done'
  | 'blocked'

export const TASK_STATUSES: readonly TaskStatus[] = [
  'backlog',
  'assigned',
  'inprogress',
  'inreview',
  'done',
  'blocked',
] as const

export const TASK_STATUS_LABELS: Record<TaskStatus, string> = {
  backlog: 'Backlog',
  assigned: 'Assigned',
  inprogress: 'In Progress',
  inreview: 'In Review',
  done: 'Done',
  blocked: 'Blocked',
}

export type AgentType = 'orchestrator' | 'worker'
export type AgentStatus = 'active' | 'inactive'

export interface ProjectResponse {
  id: string
  name: string
  description: string | null
  status: string
  createdAt: string
}

export interface TaskResponse {
  id: string
  title: string
  status: TaskStatus
  assignedAgentId: string | null
  epicId: string | null
  createdAt: string
  updatedAt: string
}

export interface TaskContextTaskResponse {
  id: string
  projectId: string
  epicId: string | null
  parentTaskId: string | null
  assignedAgentId: string | null
  title: string
  description: string
  status: TaskStatus
  blockedReason: string | null
  metadata: Record<string, string>
  createdAt: string
  updatedAt: string
}

export interface TaskContextProjectResponse {
  id: string
  name: string
}

export interface TaskContextEpicResponse {
  id: string
  title: string
  description: string
  status: string
}

export interface TaskContextParentTaskResponse {
  id: string
  title: string
  status: TaskStatus
}

export interface TaskContextSubtaskResponse {
  id: string
  title: string
  status: TaskStatus
  assignedAgentId: string | null
  assignedAgentName: string | null
  updatedAt: string
}

export interface TaskContextDependencyTaskResponse {
  taskId: string
  title: string
  status: TaskStatus
  depId: string
}

export interface TaskContextDependenciesResponse {
  blockedBy: TaskContextDependencyTaskResponse[]
  blocking: TaskContextDependencyTaskResponse[]
}

export interface TaskContextNoteResponse {
  agent: string
  agentType: string
  type: string
  content: string
  createdAt: string
}

export interface TaskContextEventResponse {
  id: string
  eventType: string
  oldValue: string | null
  newValue: string | null
  agent: string
  timestamp: string
}

export interface TaskContextDecisionResponse {
  id: string
  title: string
  content: string
  status: string
  agent: string
  createdAt: string
}

export interface TaskDetailResponse {
  task: TaskContextTaskResponse
  project: TaskContextProjectResponse
  epic: TaskContextEpicResponse | null
  parentTask: TaskContextParentTaskResponse | null
  subtasks: TaskContextSubtaskResponse[]
  dependencies: TaskContextDependenciesResponse
  notes: TaskContextNoteResponse[]
  events: TaskContextEventResponse[]
  relatedDecisions: TaskContextDecisionResponse[]
}

export interface AgentSummary {
  id: string
  name: string
  type: AgentType
  platform: string
  status: AgentStatus
  lastSeenAt: string | null
}

export interface DecisionResponse {
  id: string
  projectId: string
  taskId: string | null
  agentId: string
  agentName: string
  agentType: string
  title: string
  content: string
  status: string
  createdAt: string
}

export interface NotificationResponse {
  id: string
  type: string
  taskId: string
  taskTitle: string
  message: string
  createdAt: string
  isAcknowledged: boolean
}

export interface EpicResponse {
  id: string
  projectId: string
  title: string
  description: string | null
  status: string
  createdAt: string
  tasks: EpicTaskSummaryResponse[] | null
}

export interface EpicTaskSummaryResponse {
  id: string
  title: string
  description: string
  status: TaskStatus
  assignedAgentId: string | null
  createdAt: string
  updatedAt: string
}

export interface AdminKeyInfoResponse {
  prefix: string
  createdAt: string
  lastUsedAt: string | null
}

export interface KeyRotationResponse {
  apiKey: string
  message: string
}

export interface CurrentAgentCoordinatorResponse {
  isAdmin: true
  isCoordinator: true
  organizationId: string | null
  organizationScopeError: string | null
  message: string
}

export interface CurrentAgentWorkerResponse {
  id: string
  name: string
  type: AgentType
  platform: string
  status: AgentStatus
  organizationId: string
  lastSeenAt: string | null
  createdAt: string
  assignedTasks: Array<{
    id: string
    title: string
    status: TaskStatus
    projectId: string
    updatedAt: string
  }>
  unacknowledgedNotificationCount: number
}

export type CurrentAgentResponse =
  | CurrentAgentCoordinatorResponse
  | CurrentAgentWorkerResponse

export interface CreateProjectRequest {
  name: string
  description?: string
}

export interface CreateTaskRequest {
  title: string
  description?: string
  epicId?: string
  parentTaskId?: string
  metadata?: Record<string, string>
}

export interface UpdateTaskRequest {
  title?: string
  description?: string
  epicId?: string
  assignedAgentId?: string | null
  metadata?: Record<string, string>
}

export interface UpdateTaskStatusRequest {
  status?: string
  blockedReason?: string | null
  assignedAgentId?: string | null
}

export interface CreateNoteRequest {
  content: string
  type?: string
}

export interface CreateDecisionRequest {
  title: string
  content: string
  taskId?: string
  status?: string
}
