import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import ReactMarkdown from 'react-markdown'
import { api, usePolling } from '../api/client'
import { ErrorBanner, EmptyState, LoadingState } from '../components/PageState'
import { GenericBadge } from '../components/StatusBadge'
import { formatAbsolute } from '../lib/time'

export function DecisionLog() {
  const { id } = useParams()
  const projectId = id ?? ''

  const fetcher = useCallback(() => api.listDecisions(projectId), [projectId])
  const { data, error, loading } = usePolling(fetcher, [projectId])
  const [expandedId, setExpandedId] = useState<string | null>(null)

  return (
    <div>
      <div className="mb-4">
        <Link to={`/projects/${projectId}/board`} className="text-xs text-ink-400 hover:text-ink-200">
          ← Back to board
        </Link>
        <h1 className="text-2xl font-semibold">Decision log</h1>
        <p className="text-sm text-ink-400">
          {data
            ? `${data.length} decision${data.length === 1 ? '' : 's'} · auto-refreshing every 10s`
            : 'Loading…'}
        </p>
      </div>

      <ErrorBanner error={error} />

      {loading && !data ? <LoadingState /> : null}

      {data && data.length === 0 ? (
        <EmptyState message="No decisions recorded for this project." />
      ) : null}

      {data && data.length > 0 ? (
        <div className="space-y-3">
          {data.map((decision) => {
            const expanded = expandedId === decision.id
            return (
              <div key={decision.id} className="card">
                <button
                  type="button"
                  className="flex w-full items-start justify-between gap-3 text-left"
                  onClick={() => setExpandedId(expanded ? null : decision.id)}
                >
                  <div className="flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <h2 className="text-base font-semibold text-ink-100">
                        {decision.title}
                      </h2>
                      <DecisionStatusBadge status={decision.status} />
                    </div>
                    <p className="mt-1 text-xs text-ink-400">
                      {decision.agentName}{' '}
                      <span className="text-ink-500">({decision.agentType})</span> ·{' '}
                      {formatAbsolute(decision.createdAt)}
                    </p>
                  </div>
                  <span className="text-xs text-ink-400">{expanded ? 'Hide' : 'Show'}</span>
                </button>

                {expanded ? (
                  <div className="markdown-body mt-3 border-t border-ink-800 pt-3">
                    <ReactMarkdown>{decision.content}</ReactMarkdown>
                  </div>
                ) : null}
              </div>
            )
          })}
        </div>
      ) : null}
    </div>
  )
}

function DecisionStatusBadge({ status }: { status: string }) {
  const normalized = status.toLowerCase()
  const tone =
    normalized === 'accepted'
      ? 'positive'
      : normalized === 'superseded'
        ? 'neutral'
        : 'info'
  return <GenericBadge label={status} tone={tone} />
}
