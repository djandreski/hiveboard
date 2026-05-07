import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { ApiError, api, apiKey, useApiKey } from '../api/client'

interface AuthGateProps {
  children: ReactNode
}

export function AuthGate({ children }: AuthGateProps) {
  const storedKey = useApiKey()
  const [validated, setValidated] = useState(false)
  const [validating, setValidating] = useState(false)
  const [authError, setAuthError] = useState<string | null>(null)
  const [keyInput, setKeyInput] = useState('')

  // Validate the stored key against /agents/me whenever it changes.
  useEffect(() => {
    if (!storedKey) {
      setValidated(false)
      return
    }

    let cancelled = false
    setValidating(true)
    setAuthError(null)

    api
      .validateKey()
      .then(() => {
        if (!cancelled) {
          setValidated(true)
        }
      })
      .catch((err: unknown) => {
        if (cancelled) return
        if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
          setAuthError('That key was rejected. Please re-enter your coordinator/admin key.')
          apiKey.clear()
        } else if (err instanceof ApiError) {
          setAuthError(`Validation failed: ${err.message}`)
          apiKey.clear()
        } else {
          setAuthError('Could not reach the API. Verify VITE_API_BASE_URL or proxy settings.')
        }
        setValidated(false)
      })
      .finally(() => {
        if (!cancelled) setValidating(false)
      })

    return () => {
      cancelled = true
    }
  }, [storedKey])

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmed = keyInput.trim()
    if (!trimmed) return
    setAuthError(null)
    apiKey.set(trimmed)
    setKeyInput('')
  }

  if (storedKey && validated) {
    return <>{children}</>
  }

  return (
    <div className="flex min-h-full items-center justify-center px-4 py-12">
      <div className="w-full max-w-md">
        <div className="card">
          <h1 className="text-xl font-semibold">Connect to Hiveboard</h1>
          <p className="mt-2 text-sm text-ink-300">
            Paste your coordinator/admin API key to continue. The key is kept in
            session storage only — it is cleared when you close the tab.
          </p>

          <form className="mt-4 space-y-3" onSubmit={handleSubmit}>
            <label className="block">
              <span className="label">API key</span>
              <input
                type="password"
                autoFocus
                autoComplete="off"
                spellCheck={false}
                placeholder="hb_admin_..."
                value={keyInput}
                onChange={(event) => setKeyInput(event.target.value)}
                className="input mt-1 font-mono"
              />
            </label>

            {authError ? (
              <div className="rounded-md border border-rose-700/40 bg-rose-950/60 p-3 text-sm text-rose-200">
                {authError}
              </div>
            ) : null}

            {validating ? (
              <div className="text-xs text-ink-400">Validating…</div>
            ) : null}

            <button
              type="submit"
              className="btn btn-primary w-full"
              disabled={!keyInput.trim() || validating}
            >
              {validating ? 'Validating…' : 'Connect'}
            </button>
          </form>

          <p className="mt-4 text-xs text-ink-500">
            The dashboard talks to <code className="font-mono">/api/v1</code> on
            the configured base URL. Set <code className="font-mono">VITE_API_BASE_URL</code>
            {' '}for cross-origin deployments.
          </p>
        </div>
      </div>
    </div>
  )
}
