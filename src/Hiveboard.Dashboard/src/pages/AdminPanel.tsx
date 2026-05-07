import { useCallback, useState, type ReactNode } from 'react'
import { ApiError, api, usePolling } from '../api/client'
import type { AdminKeyInfoResponse, AgentSummary } from '../api/types'
import { ErrorBanner, EmptyState, LoadingState } from '../components/PageState'
import { GenericBadge } from '../components/StatusBadge'
import { KeyRevealModal } from '../components/KeyRevealModal'
import { Modal } from '../components/Modal'
import { formatAbsolute, formatRelative } from '../lib/time'

interface AdminData {
  keyInfo: AdminKeyInfoResponse
  agents: AgentSummary[]
}

interface RevealedKey {
  title: string
  apiKey: string
  message: string
}

const ADMIN_KEY_PREFIX_LENGTH = 12

const fetchAdminData = async (): Promise<AdminData> => {
  const [keyInfo, agents] = await Promise.all([api.getAdminKeyInfo(), api.listAgents()])
  return { keyInfo, agents }
}

export function AdminPanel() {
  const fetcher = useCallback(fetchAdminData, [])
  const { data, error, loading, refetch } = usePolling(fetcher, [], 30_000)
  const [revealed, setRevealed] = useState<RevealedKey | null>(null)
  const [confirmAdminRotate, setConfirmAdminRotate] = useState(false)
  const [confirmAgentRotate, setConfirmAgentRotate] = useState<AgentSummary | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function rotateAdmin() {
    setBusy(true)
    setActionError(null)
    try {
      const response = await api.rotateAdminKey()
      setConfirmAdminRotate(false)
      setRevealed({
        title: 'New admin API key',
        apiKey: response.apiKey,
        message: `${response.message} Re-authenticate with the new key after saving it.`,
      })
      await refetch()
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'Could not rotate admin key')
    } finally {
      setBusy(false)
    }
  }

  async function rotateAgent(agent: AgentSummary) {
    setBusy(true)
    setActionError(null)
    try {
      const response = await api.rotateAgentKey(agent.id)
      setConfirmAgentRotate(null)
      setRevealed({
        title: `New API key for ${agent.name}`,
        apiKey: response.apiKey,
        message: response.message,
      })
      await refetch()
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : 'Could not rotate agent key')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Admin</h1>
        <p className="text-sm text-ink-400">
          Key management for the coordinator/admin credential and registered agents.
        </p>
      </div>

      <ErrorBanner error={error} />
      {actionError ? (
        <div className="rounded-md border border-rose-700/50 bg-rose-950/60 p-3 text-sm text-rose-200">
          {actionError}
        </div>
      ) : null}

      {loading && !data ? <LoadingState /> : null}

      {data ? (
        <>
          <section className="card">
            <header className="mb-3">
              <h2 className="text-base font-semibold text-ink-100">Admin key</h2>
              <p className="text-xs text-ink-400">
                The shared coordinator credential. Rotating invalidates the previous key
                immediately.
              </p>
            </header>

            <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-3">
              <Field label="Prefix">
                <code className="font-mono text-ink-100">
                  {data.keyInfo.prefix.slice(0, ADMIN_KEY_PREFIX_LENGTH)}…
                </code>
              </Field>
              <Field label="Created">
                <span title={formatAbsolute(data.keyInfo.createdAt)}>
                  {formatRelative(data.keyInfo.createdAt)}
                </span>
              </Field>
              <Field label="Last used">
                <span title={formatAbsolute(data.keyInfo.lastUsedAt)}>
                  {data.keyInfo.lastUsedAt ? formatRelative(data.keyInfo.lastUsedAt) : 'Never'}
                </span>
              </Field>
            </dl>

            <div className="mt-4">
              <button
                type="button"
                className="btn btn-danger"
                onClick={() => setConfirmAdminRotate(true)}
                disabled={busy}
              >
                Rotate admin key
              </button>
            </div>
          </section>

          <section className="card">
            <header className="mb-3 flex items-center justify-between">
              <div>
                <h2 className="text-base font-semibold text-ink-100">Agent keys</h2>
                <p className="text-xs text-ink-400">
                  Rotate any individual agent's credential. The new key is shown once.
                </p>
              </div>
              <span className="badge bg-ink-800 text-ink-200">{data.agents.length}</span>
            </header>

            {data.agents.length === 0 ? (
              <EmptyState message="No agents registered yet." />
            ) : (
              <div className="overflow-hidden rounded-md border border-ink-800">
                <table className="w-full divide-y divide-ink-800 text-sm">
                  <thead className="bg-ink-900 text-ink-300">
                    <tr>
                      <th className="px-3 py-2 text-left font-medium">Name</th>
                      <th className="px-3 py-2 text-left font-medium">Type</th>
                      <th className="px-3 py-2 text-left font-medium">Platform</th>
                      <th className="px-3 py-2 text-left font-medium">Status</th>
                      <th className="px-3 py-2 text-left font-medium">Key prefix</th>
                      <th className="px-3 py-2 text-right font-medium">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-ink-800 bg-ink-950">
                    {data.agents.map((agent) => (
                      <tr key={agent.id}>
                        <td className="px-3 py-2 font-medium text-ink-100">{agent.name}</td>
                        <td className="px-3 py-2 capitalize text-ink-300">{agent.type}</td>
                        <td className="px-3 py-2 capitalize text-ink-300">{agent.platform}</td>
                        <td className="px-3 py-2">
                          <GenericBadge
                            label={agent.status}
                            tone={agent.status === 'active' ? 'positive' : 'neutral'}
                          />
                        </td>
                        <td className="px-3 py-2 font-mono text-xs text-ink-300">
                          <span title={`Agent ID: ${agent.id}`}>
                            {agent.id.slice(0, 8)}…
                          </span>
                        </td>
                        <td className="px-3 py-2 text-right">
                          <button
                            type="button"
                            className="btn"
                            onClick={() => setConfirmAgentRotate(agent)}
                            disabled={busy}
                          >
                            Rotate key
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      ) : null}

      <Modal
        open={confirmAdminRotate}
        onClose={() => setConfirmAdminRotate(false)}
        title="Rotate admin key?"
        footer={
          <>
            <button
              type="button"
              className="btn"
              onClick={() => setConfirmAdminRotate(false)}
              disabled={busy}
            >
              Cancel
            </button>
            <button
              type="button"
              className="btn btn-danger"
              onClick={rotateAdmin}
              disabled={busy}
            >
              {busy ? 'Rotating…' : 'Rotate now'}
            </button>
          </>
        }
      >
        <p className="text-sm text-ink-200">
          The current admin key will be invalidated immediately. Any clients using it
          must be reconfigured with the new key.
        </p>
      </Modal>

      <Modal
        open={!!confirmAgentRotate}
        onClose={() => setConfirmAgentRotate(null)}
        title={`Rotate key for ${confirmAgentRotate?.name ?? ''}?`}
        footer={
          <>
            <button
              type="button"
              className="btn"
              onClick={() => setConfirmAgentRotate(null)}
              disabled={busy}
            >
              Cancel
            </button>
            <button
              type="button"
              className="btn btn-danger"
              onClick={() => confirmAgentRotate && rotateAgent(confirmAgentRotate)}
              disabled={busy}
            >
              {busy ? 'Rotating…' : 'Rotate now'}
            </button>
          </>
        }
      >
        <p className="text-sm text-ink-200">
          The current API key for this agent will be invalidated immediately. The agent
          will need its new key to authenticate.
        </p>
      </Modal>

      <KeyRevealModal
        open={!!revealed}
        onClose={() => setRevealed(null)}
        title={revealed?.title ?? ''}
        apiKey={revealed?.apiKey ?? ''}
        message={revealed?.message}
      />
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="label">{label}</dt>
      <dd className="mt-1 text-ink-100">{children}</dd>
    </div>
  )
}
