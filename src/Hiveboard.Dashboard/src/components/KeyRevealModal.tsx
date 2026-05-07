import { useState } from 'react'
import { Modal } from './Modal'

interface KeyRevealModalProps {
  open: boolean
  onClose: () => void
  title: string
  apiKey: string
  message?: string
}

export function KeyRevealModal({ open, onClose, title, apiKey, message }: KeyRevealModalProps) {
  const [copied, setCopied] = useState(false)

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(apiKey)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 2_000)
    } catch {
      // Clipboard write failed (permissions, etc.) — leave the user to manual select.
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      size="lg"
      footer={
        <>
          <button type="button" onClick={handleCopy} className="btn">
            {copied ? 'Copied!' : 'Copy to clipboard'}
          </button>
          <button type="button" onClick={onClose} className="btn btn-primary">
            I saved it
          </button>
        </>
      }
    >
      <div className="space-y-3">
        <div className="rounded-md border border-amber-700/40 bg-amber-950/40 p-3 text-sm text-amber-200">
          This key is shown once and cannot be retrieved again. Save it now in
          your password manager or secrets store.
        </div>

        <div>
          <span className="label">New API key</span>
          <pre className="mt-1 max-h-40 overflow-auto rounded-md border border-ink-700 bg-ink-950 p-3 font-mono text-xs text-ink-100">
            {apiKey}
          </pre>
        </div>

        {message ? <p className="text-xs text-ink-400">{message}</p> : null}
      </div>
    </Modal>
  )
}
