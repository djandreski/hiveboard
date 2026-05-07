import { type ReactNode } from 'react'
import { NavLink, useParams } from 'react-router-dom'
import { apiKey } from '../api/client'

interface LayoutProps {
  children: ReactNode
}

const baseNav = [
  { to: '/', label: 'Projects', end: true },
  { to: '/agents', label: 'Agents' },
  { to: '/admin', label: 'Admin' },
]

const projectNav = (id: string) => [
  { to: `/projects/${id}/board`, label: 'Board' },
  { to: `/projects/${id}/timeline`, label: 'Timeline' },
  { to: `/projects/${id}/decisions`, label: 'Decisions' },
  { to: `/projects/${id}/console`, label: 'Console' },
]

export function Layout({ children }: LayoutProps) {
  const params = useParams()
  const projectId = params.id

  return (
    <div className="flex min-h-full flex-col">
      <header className="border-b border-ink-800 bg-ink-950/80 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center gap-6 px-4 py-3">
          <NavLink to="/" className="flex items-center gap-2 text-lg font-semibold text-ink-100">
            <span className="inline-block h-2.5 w-2.5 rounded-full bg-accent" />
            Hiveboard
          </NavLink>

          <nav className="flex flex-1 items-center gap-1 text-sm">
            {baseNav.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  `rounded-md px-3 py-1.5 transition ${
                    isActive
                      ? 'bg-ink-800 text-ink-100'
                      : 'text-ink-300 hover:bg-ink-900 hover:text-ink-100'
                  }`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          <button
            type="button"
            onClick={() => apiKey.clear()}
            className="text-xs text-ink-400 underline-offset-4 hover:text-ink-200 hover:underline"
            title="Clear the session API key"
          >
            Sign out
          </button>
        </div>

        {projectId ? (
          <div className="border-t border-ink-800 bg-ink-900/50">
            <div className="mx-auto flex max-w-7xl items-center gap-1 px-4 py-2 text-sm">
              <span className="label mr-2">Project</span>
              {projectNav(projectId).map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) =>
                    `rounded-md px-3 py-1 transition ${
                      isActive
                        ? 'bg-accent/20 text-accent'
                        : 'text-ink-300 hover:bg-ink-800 hover:text-ink-100'
                    }`
                  }
                >
                  {item.label}
                </NavLink>
              ))}
            </div>
          </div>
        ) : null}
      </header>

      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6">{children}</main>
    </div>
  )
}
