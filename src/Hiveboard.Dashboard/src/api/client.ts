import { useEffect, useRef, useState } from 'react'
import type {
  AdminKeyInfoResponse,
  AgentSummary,
  CreateDecisionRequest,
  CreateNoteRequest,
  CreateProjectRequest,
  CreateTaskRequest,
  CurrentAgentResponse,
  DecisionResponse,
  EpicResponse,
  KeyRotationResponse,
  NotificationResponse,
  ProjectResponse,
  TaskDetailResponse,
  TaskResponse,
  UpdateTaskRequest,
  UpdateTaskStatusRequest,
} from './types'

const API_KEY_STORAGE_KEY = 'hiveboard.apiKey'

export class ApiError extends Error {
  status: number
  body: unknown

  constructor(status: number, message: string, body: unknown) {
    super(message)
    this.status = status
    this.body = body
  }
}

function readSessionKey(): string | null {
  if (typeof sessionStorage === 'undefined') return null
  return sessionStorage.getItem(API_KEY_STORAGE_KEY)
}

function writeSessionKey(key: string): void {
  if (typeof sessionStorage === 'undefined') return
  sessionStorage.setItem(API_KEY_STORAGE_KEY, key)
}

function clearSessionKey(): void {
  if (typeof sessionStorage === 'undefined') return
  sessionStorage.removeItem(API_KEY_STORAGE_KEY)
}

const apiKeyListeners = new Set<(key: string | null) => void>()

function notifyApiKeyChange(key: string | null): void {
  for (const listener of apiKeyListeners) {
    try {
      listener(key)
    } catch {
      // Ignore listener errors so one bad subscriber can't break others.
    }
  }
}

export const apiKey = {
  get: readSessionKey,
  set(key: string): void {
    writeSessionKey(key)
    notifyApiKeyChange(key)
  },
  clear(): void {
    clearSessionKey()
    notifyApiKeyChange(null)
  },
  subscribe(listener: (key: string | null) => void): () => void {
    apiKeyListeners.add(listener)
    return () => apiKeyListeners.delete(listener)
  },
}

function getBaseUrl(): string {
  // Empty string means "same origin" — used in dev (with the Vite proxy)
  // and when bundled into the API host.
  const explicit = import.meta.env.VITE_API_BASE_URL
  return typeof explicit === 'string' ? explicit.replace(/\/$/, '') : ''
}

async function request<T>(
  path: string,
  init: RequestInit & { json?: unknown } = {},
): Promise<T> {
  const url = `${getBaseUrl()}${path}`
  const headers = new Headers(init.headers ?? {})
  const key = readSessionKey()
  if (key && !headers.has('X-Api-Key')) {
    headers.set('X-Api-Key', key)
  }

  let body = init.body
  if (init.json !== undefined) {
    headers.set('Content-Type', 'application/json')
    body = JSON.stringify(init.json)
  }

  const response = await fetch(url, { ...init, headers, body })
  const isJson = response.headers.get('Content-Type')?.includes('application/json')
  const payload = isJson ? await response.json().catch(() => null) : null

  if (!response.ok) {
    const message =
      (payload && typeof payload === 'object' && 'error' in payload
        ? String((payload as { error: unknown }).error)
        : null) ??
      response.statusText ??
      `Request failed with status ${response.status}`
    throw new ApiError(response.status, message, payload)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return payload as T
}

export const api = {
  // Auth helpers
  validateKey: (): Promise<CurrentAgentResponse> =>
    request<CurrentAgentResponse>('/api/v1/agents/me'),

  // Projects
  listProjects: (): Promise<ProjectResponse[]> =>
    request<ProjectResponse[]>('/api/v1/projects'),
  getProject: (id: string): Promise<ProjectResponse> =>
    request<ProjectResponse>(`/api/v1/projects/${id}`),
  createProject: (body: CreateProjectRequest): Promise<ProjectResponse> =>
    request<ProjectResponse>('/api/v1/projects', { method: 'POST', json: body }),

  // Epics
  listEpics: (projectId: string): Promise<EpicResponse[]> =>
    request<EpicResponse[]>(`/api/v1/projects/${projectId}/epics`),

  // Agents
  listAgents: (): Promise<AgentSummary[]> =>
    request<AgentSummary[]>('/api/v1/agents'),
  rotateAgentKey: (id: string): Promise<KeyRotationResponse> =>
    request<KeyRotationResponse>(`/api/v1/agents/${id}/keys/rotate`, {
      method: 'POST',
    }),

  // Tasks
  listTasks: (projectId: string, status?: string): Promise<TaskResponse[]> => {
    const qs = status ? `?status=${encodeURIComponent(status)}` : ''
    return request<TaskResponse[]>(`/api/v1/projects/${projectId}/tasks${qs}`)
  },
  getTask: (id: string): Promise<TaskDetailResponse> =>
    request<TaskDetailResponse>(`/api/v1/tasks/${id}`),
  createTask: (
    projectId: string,
    body: CreateTaskRequest,
  ): Promise<TaskResponse> =>
    request<TaskResponse>(`/api/v1/projects/${projectId}/tasks`, {
      method: 'POST',
      json: body,
    }),
  updateTask: (id: string, body: UpdateTaskRequest): Promise<TaskResponse> =>
    request<TaskResponse>(`/api/v1/tasks/${id}`, { method: 'PATCH', json: body }),
  updateTaskStatus: (
    id: string,
    body: UpdateTaskStatusRequest,
  ): Promise<TaskResponse> =>
    request<TaskResponse>(`/api/v1/tasks/${id}/status`, {
      method: 'PATCH',
      json: body,
    }),

  // Notes
  createNote: (taskId: string, body: CreateNoteRequest): Promise<unknown> =>
    request<unknown>(`/api/v1/tasks/${taskId}/notes`, {
      method: 'POST',
      json: body,
    }),

  // Decisions
  listDecisions: (projectId: string): Promise<DecisionResponse[]> =>
    request<DecisionResponse[]>(`/api/v1/projects/${projectId}/decisions`),
  createDecision: (
    projectId: string,
    body: CreateDecisionRequest,
  ): Promise<DecisionResponse> =>
    request<DecisionResponse>(`/api/v1/projects/${projectId}/decisions`, {
      method: 'POST',
      json: body,
    }),

  // Notifications
  listMyNotifications: (): Promise<NotificationResponse[]> =>
    request<NotificationResponse[]>('/api/v1/agents/me/notifications'),

  // Admin
  getAdminKeyInfo: (): Promise<AdminKeyInfoResponse> =>
    request<AdminKeyInfoResponse>('/api/v1/admin/keys/info'),
  rotateAdminKey: (): Promise<KeyRotationResponse> =>
    request<KeyRotationResponse>('/api/v1/admin/keys/rotate', { method: 'POST' }),
}

export interface PollingState<T> {
  data: T | undefined
  error: ApiError | null
  loading: boolean
  refetch: () => Promise<void>
}

/**
 * Re-runs `fetcher` immediately and then on a fixed interval.
 *
 * `deps` controls when the fetcher is reset; pass values that uniquely
 * identify the request (e.g. path params). Pass `null` as the fetcher
 * to disable polling entirely.
 */
export function usePolling<T>(
  fetcher: (() => Promise<T>) | null,
  deps: ReadonlyArray<unknown> = [],
  intervalMs = 10_000,
): PollingState<T> {
  const [data, setData] = useState<T | undefined>(undefined)
  const [error, setError] = useState<ApiError | null>(null)
  const [loading, setLoading] = useState<boolean>(fetcher !== null)
  const fetcherRef = useRef(fetcher)
  fetcherRef.current = fetcher

  const tickRef = useRef<() => Promise<void>>(async () => {})

  useEffect(() => {
    let cancelled = false

    async function tick() {
      const current = fetcherRef.current
      if (!current) return
      try {
        const result = await current()
        if (cancelled) return
        setData(result)
        setError(null)
      } catch (err) {
        if (cancelled) return
        if (err instanceof ApiError) {
          setError(err)
        } else {
          setError(new ApiError(0, (err as Error)?.message ?? 'Request failed', err))
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    tickRef.current = tick

    if (fetcher === null) {
      setLoading(false)
      return
    }

    setLoading(true)
    void tick()
    const id = window.setInterval(tick, intervalMs)
    return () => {
      cancelled = true
      window.clearInterval(id)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [intervalMs, fetcher === null, ...deps])

  return {
    data,
    error,
    loading,
    refetch: () => tickRef.current(),
  }
}

/**
 * Subscribes to API key changes from session storage so components can
 * react to login/logout transitions.
 */
export function useApiKey(): string | null {
  const [key, setKey] = useState<string | null>(() => apiKey.get())

  useEffect(() => apiKey.subscribe(setKey), [])

  return key
}
