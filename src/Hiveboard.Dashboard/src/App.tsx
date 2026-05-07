import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthGate } from './components/AuthGate'
import { Layout } from './components/Layout'
import { ProjectOverview } from './pages/ProjectOverview'
import { TaskBoard } from './pages/TaskBoard'
import { AgentActivity } from './pages/AgentActivity'
import { EventTimeline } from './pages/EventTimeline'
import { DecisionLog } from './pages/DecisionLog'
import { CoordinatorConsole } from './pages/CoordinatorConsole'
import { AdminPanel } from './pages/AdminPanel'

function App() {
  // BASE_URL ends in '/' from Vite (e.g. '/' in dev, '/dashboard/' in bundle).
  // React Router's basename should not include the trailing slash.
  const basename = (import.meta.env.BASE_URL ?? '/').replace(/\/$/, '') || '/'

  return (
    <BrowserRouter basename={basename}>
      <AuthGate>
        <Layout>
          <Routes>
            <Route path="/" element={<ProjectOverview />} />
            <Route path="/projects/:id/board" element={<TaskBoard />} />
            <Route path="/projects/:id/timeline" element={<EventTimeline />} />
            <Route path="/projects/:id/decisions" element={<DecisionLog />} />
            <Route path="/projects/:id/console" element={<CoordinatorConsole />} />
            <Route path="/agents" element={<AgentActivity />} />
            <Route path="/admin" element={<AdminPanel />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </Layout>
      </AuthGate>
    </BrowserRouter>
  )
}

export default App
