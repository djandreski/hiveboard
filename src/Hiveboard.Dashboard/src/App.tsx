import './App.css'

function App() {
  return (
    <main className="control-plane">
      <section className="hero">
        <p className="eyebrow">Hiveboard Dashboard</p>
        <h1>Coordinator-first control plane</h1>
        <p className="intro">
          The MVP now treats the human coordinator as the default controlling actor.
          Use the REST API and Swagger to manage projects, epics, tasks, and worker
          assignment while the interactive dashboard catches up.
        </p>
        <div className="status-strip">
          <span>REST API: primary surface</span>
          <span>Vite app: standalone</span>
          <span>Bundling: opt-in</span>
        </div>
      </section>

      <section className="grid">
        <article className="panel emphasis">
          <h2>Current focus</h2>
          <p>Coordinator-managed CRUD and assignment flows are active in the API.</p>
          <dl>
            <div>
              <dt>Projects</dt>
              <dd>Create without attaching an orchestrator by default.</dd>
            </div>
            <div>
              <dt>Epics</dt>
              <dd>Organize project scope from the same coordinator credential.</dd>
            </div>
            <div>
              <dt>Tasks</dt>
              <dd>Assign workers directly from the control plane.</dd>
            </div>
          </dl>
        </article>

        <article className="panel">
          <h2>Runtime notes</h2>
          <ul>
            <li>Expected API host routes: <code>/swagger</code> and <code>/health</code></li>
            <li>The coordinator/admin key is the self-hosted control-plane credential.</li>
            <li>Orchestrator agents still work, but they are optional for normal CRUD.</li>
          </ul>
        </article>

        <article className="panel">
          <h2>Next dashboard steps</h2>
          <ul>
            <li>Project overview and task board</li>
            <li>Assignment, review, and blocker controls</li>
            <li>Agent activity and decision log views</li>
          </ul>
        </article>
      </section>
    </main>
  )
}

export default App
