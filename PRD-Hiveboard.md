# Hiveboard — Product Requirements Document

## Headless Project Management for AI Coding Agents

**Version:** 1.0 (MVP)
**Date:** March 13, 2026
**Status:** Draft

---

## 1. Overview

### 1.1 Product Vision

Hiveboard is a headless, API-first project management system purpose-built for AI coding agents. Unlike traditional PM tools designed around human interaction, Hiveboard is optimized for machine-to-machine communication — enabling multiple AI agents to collaborate on software projects through structured task management, dependency tracking, and shared context.

### 1.2 Problem Statement

As AI coding agents (GitHub Copilot, Claude Code, Cursor, OpenAI Codex) become integral to development workflows, teams increasingly run multiple agents concurrently on the same project. There is no purpose-built system for:

- **Sequencing dependent tasks** across multiple agents working in parallel
- **Sharing context and decisions** so agents don't duplicate work or make contradictory choices
- **Coordinating work** to prevent conflicts and ensure coherent output
- **Providing visibility** to humans overseeing agent-driven development

Today, developers cobble together GitHub Issues, markdown files, and manual prompts to coordinate agents. Hiveboard replaces this with a structured, agent-native system.

### 1.3 Product Name

**Hiveboard** — hive mind + board, emphasizing multi-agent collaboration.

### 1.4 Business Model

**Open-core:**

- **Free (open-source core):** Self-hosted, single-organization, SQLite storage, REST + MCP API, read-only dashboard
- **Paid (cloud/enterprise):** Cloud-hosted, multi-organization, PostgreSQL, advanced analytics, audit logs, SSO, priority support

---

## 2. Target Users

### 2.1 Primary Personas

| Persona | Description | Key Need |
|---|---|---|
| **Solo Developer** | Runs 2–5 AI agents on personal/side projects | Coordinate agents without babysitting; see what's happening |
| **Dev Team** | 3–15 person team using agents for feature work | Prevent agent conflicts; maintain architectural coherence |
| **Orchestrator Agent** | An AI agent (external) that plans and assigns work | Programmatic task creation, assignment, and monitoring |
| **Worker Agent** | An AI agent that executes assigned tasks | Retrieve task details, report progress, share context |

### 2.2 Non-Users (Explicitly Out of Scope)

- Human project managers using this as a traditional PM tool
- Non-coding workflows (marketing, design, etc.)

---

## 3. Architecture

### 3.1 Deployment Models

| Model | Storage | Use Case |
|---|---|---|
| **Self-hosted (local)** | SQLite | Solo developers, local agent workflows |
| **Cloud-hosted** | PostgreSQL | Teams, persistent projects, shared access |

### 3.2 System Architecture

```
┌─────────────────────────────────────────────────────┐
│                   Hiveboard Server                    │
│                                                      │
│  ┌──────────┐  ┌──────────┐  ┌───────────────────┐  │
│  │ REST API │  │ MCP API  │  │  Web Dashboard    │  │
│  │ (primary)│  │ (agents) │  │  (read-only)      │  │
│  └────┬─────┘  └────┬─────┘  └────────┬──────────┘  │
│       │              │                 │             │
│  ┌────┴──────────────┴─────────────────┴──────────┐  │
│  │              Core Engine                        │  │
│  │  ┌──────────┐ ┌────────────┐ ┌──────────────┐  │  │
│  │  │ Task     │ │ Dependency │ │ Notification │  │  │
│  │  │ Manager  │ │ Resolver   │ │ Engine       │  │  │
│  │  └──────────┘ └────────────┘ └──────────────┘  │  │
│  │  ┌──────────┐ ┌────────────┐ ┌──────────────┐  │  │
│  │  │ Context  │ │ Decision   │ │ Event        │  │  │
│  │  │ Store    │ │ Log        │ │ Journal      │  │  │
│  │  └──────────┘ └────────────┘ └──────────────┘  │  │
│  └─────────────────────────────────────────────────┘  │
│                        │                              │
│  ┌─────────────────────┴───────────────────────────┐  │
│  │           Storage Layer (SQLite / PostgreSQL)    │  │
│  └─────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
         ▲              ▲              ▲
         │              │              │
    ┌────┴───┐    ┌─────┴────┐   ┌────┴────┐
    │Orchest.│    │ Worker   │   │ Worker  │
    │ Agent  │    │ Agent 1  │   │ Agent 2 │
    └────────┘    └──────────┘   └─────────┘
```

### 3.3 Multi-Tenancy

- **Organization** is the top-level tenant
- Each organization has multiple **Projects**
- Each project has exactly **one Orchestrator Agent** and multiple **Worker Agents**
- Agent API keys are scoped to an organization

### 3.4 Technology Stack

| Component | Technology |
|---|---|
| Language | C# / .NET 10 |
| API Framework | ASP.NET Core Minimal API |
| Database (local) | SQLite via EF Core |
| Database (cloud) | PostgreSQL via EF Core |
| MCP Server | .NET MCP SDK |
| Dashboard | React SPA (served as static files by the API host) |
| Authentication | API key-based |
| Serialization | System.Text.Json |

---

## 4. Data Model

### 4.1 Entity Relationship

```
Organization (1) ──── (*) Project
Project (1) ──── (*) Epic
Project (1) ──── (*) Task
Project (1) ──── (*) DecisionRecord
Project (1) ──── (1) OrchestratorAgent
Project (1) ──── (*) WorkerAgent

Epic (1) ──── (*) Task
Task (1) ──── (*) Task (subtasks — decomposition)
Task (1) ──── (*) TaskDependency
Task (1) ──── (*) TaskNote
Task (1) ──── (*) TaskEvent (audit trail)
Task (1) ──── (0..1) Agent (assigned)
```

### 4.2 Core Entities

#### Organization

| Field | Type | Description |
|---|---|---|
| `id` | UUID | Unique identifier |
| `name` | string | Organization name |
| `created_at` | datetime | Creation timestamp |

#### Project

| Field | Type | Description |
|---|---|---|
| `id` | UUID | Unique identifier |
| `organization_id` | UUID | Parent organization |
| `name` | string | Project name |
| `description` | string | Project description / goals |
| `status` | enum | `active`, `archived` |
| `created_at` | datetime | Creation timestamp |

#### Agent

| Field | Type | Description |
|---|---|---|
| `id` | UUID | Unique identifier |
| `organization_id` | UUID | Parent organization |
| `name` | string | Agent display name |
| `type` | enum | `orchestrator`, `worker` |
| `agent_platform` | string | `copilot`, `claude-code`, `cursor`, `codex`, `custom` |
| `api_key_hash` | string | Hashed API key for authentication |
| `status` | enum | `active`, `inactive` |
| `last_seen_at` | datetime | Last API interaction |
| `created_at` | datetime | Creation timestamp |

#### Epic

| Field | Type | Description |
|---|---|---|
| `id` | UUID | Unique identifier |
| `project_id` | UUID | Parent project |
| `title` | string | Epic title |
| `description` | string | Epic description (markdown) |
| `status` | enum | `open`, `in-progress`, `done` |
| `created_at` | datetime | Creation timestamp |

#### Task

| Field | Type | Description |
|---|---|---|
| `id` | UUID | Unique identifier |
| `project_id` | UUID | Parent project |
| `epic_id` | UUID? | Optional parent epic |
| `parent_task_id` | UUID? | Parent task (for subtask decomposition) |
| `assigned_agent_id` | UUID? | Assigned worker agent |
| `title` | string | Task title |
| `description` | string | Task description (markdown) |
| `status` | enum | `backlog`, `assigned`, `in-progress`, `in-review`, `done`, `blocked` |
| `blocked_reason` | string? | Why the task is blocked |
| `metadata` | JSON | Flexible metadata (branch name, PR URL, etc.) |
| `created_at` | datetime | Creation timestamp |
| `updated_at` | datetime | Last update timestamp |

**Constraints:**
- A task can only be assigned to **one agent** at a time
- Only the orchestrator agent can assign tasks
- When status changes to `blocked`, the system fires a notification to the orchestrator

#### TaskDependency

| Field | Type | Description |
|---|---|---|
| `id` | UUID | Unique identifier |
| `task_id` | UUID | The dependent task (blocked until dependency is done) |
| `depends_on_task_id` | UUID | The dependency (must complete first) |
| `type` | enum | `blocks`, `required-by` |

**Constraints:**
- Circular dependency detection on creation
- A task cannot transition to `in-progress` if any `blocks` dependency is not `done`

#### TaskNote

| Field | Type | Description |
|---|---|---|
| `id` | UUID | Unique identifier |
| `task_id` | UUID | Parent task |
| `agent_id` | UUID | Agent that created the note |
| `content` | string | Note content (markdown) |
| `note_type` | enum | `context`, `progress`, `review-request`, `blocker`, `resolution` |
| `created_at` | datetime | Creation timestamp |

#### TaskEvent (Audit Trail)

| Field | Type | Description |
|---|---|---|
| `id` | UUID | Unique identifier |
| `task_id` | UUID | Parent task |
| `agent_id` | UUID | Agent that triggered the event |
| `event_type` | string | `created`, `assigned`, `status_changed`, `note_added`, `dependency_added`, `decomposed` |
| `old_value` | string? | Previous value |
| `new_value` | string? | New value |
| `timestamp` | datetime | When it happened |

#### DecisionRecord

| Field | Type | Description |
|---|---|---|
| `id` | UUID | Unique identifier |
| `project_id` | UUID | Parent project |
| `task_id` | UUID? | Optional related task |
| `agent_id` | UUID | Agent that recorded the decision |
| `title` | string | Decision title |
| `content` | string | Free-form markdown (rationale, alternatives considered, rejected approaches) |
| `status` | enum | `proposed`, `accepted`, `superseded` |
| `created_at` | datetime | Creation timestamp |

---

## 5. API Design

### 5.1 Authentication

All API requests require an `X-Api-Key` header. The API key identifies both the agent and the organization. The system determines agent role (orchestrator vs. worker) from the key.

```
X-Api-Key: hb_sk_abc123...
```

### 5.2 Agent Registration & Onboarding

Before an agent can interact with Hiveboard, it must be registered. Registration is an **admin operation** — performed by a human or setup script, not by the agents themselves.

#### Registration Flow

```
1. Human/setup script calls POST /api/v1/agents/register with an Admin API Key
   Body: { "name": "claude-worker-1", "type": "worker", "platform": "claude-code", "organizationId": "..." }

2. Hiveboard generates a unique API key (hb_sk_...), hashes it with SHA-256, and stores the hash

3. The response returns the plaintext API key ONCE — it is never stored or retrievable again:
   { "agentId": "...", "apiKey": "hb_sk_abc123...", "name": "claude-worker-1", "type": "worker" }

4. The human configures the agent with this API key:
   - For MCP agents: set HIVEBOARD_API_KEY in the MCP server config
   - For REST agents: include the key in X-Api-Key header

5. The agent calls GET /api/v1/agents/me to verify its identity and see assigned tasks

6. The agent is now visible to orchestrators and can receive task assignments
```

#### Admin API Key

A special **Admin API Key** is generated on first startup and printed to the console / log. This key is used exclusively for agent management operations (register, update, deactivate). It is not tied to any agent — it represents the human administrator.

The admin key can also be set via environment variable `HIVEBOARD_ADMIN_KEY` for automated deployments.

#### Agent Lifecycle

| State | Description |
|---|---|
| **Registered** | Agent is created, API key issued, but hasn't made any API call yet |
| **Active** | Agent has made at least one API call (`last_seen_at` is set) |
| **Inactive** | Agent has been deactivated by admin — API key is revoked, all calls return 401 |

#### Orchestrator Assignment to Project

After registration, an orchestrator agent is linked to a project when it creates or is assigned to one. Each project has exactly one orchestrator. When the orchestrator creates a project via `POST /projects`, it automatically becomes that project's orchestrator.

#### Worker Availability

Once registered, worker agents become available to orchestrators. The orchestrator discovers available workers by calling `GET /agents` (filtered to workers in their organization). Workers don't need to explicitly "join" a project — the orchestrator assigns them tasks, which implicitly links them.

### 5.3 REST API Endpoints

**Base URL:** `/api/v1`

#### Projects

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/projects` | Any | List projects in the organization |
| `GET` | `/projects/{id}` | Any | Get project details |
| `POST` | `/projects` | Orchestrator | Create a new project |

#### Epics

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/projects/{pid}/epics` | Any | List epics |
| `POST` | `/projects/{pid}/epics` | Orchestrator | Create an epic |
| `GET` | `/epics/{id}` | Any | Get epic with tasks |

#### Tasks

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/projects/{pid}/tasks` | Any | List tasks (filterable by status, agent, epic) |
| `POST` | `/projects/{pid}/tasks` | Orchestrator | Create a task |
| `GET` | `/tasks/{id}` | Any | Get full task context (description, notes, dependencies, events) |
| `PATCH` | `/tasks/{id}` | Orchestrator | Update task (assign, change description) |
| `PATCH` | `/tasks/{id}/status` | Assigned Agent | Transition task status |
| `POST` | `/tasks/{id}/subtasks` | Assigned Agent | Decompose task into subtasks |
| `POST` | `/tasks/{id}/notes` | Any | Add a note to a task |
| `GET` | `/tasks/{id}/notes` | Any | List notes on a task |

#### Dependencies

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/tasks/{id}/dependencies` | Orchestrator | Add a dependency |
| `DELETE` | `/tasks/{id}/dependencies/{dep_id}` | Orchestrator | Remove a dependency |
| `GET` | `/projects/{pid}/dependencies/graph` | Any | Get the full dependency graph |

#### Decision Records

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/projects/{pid}/decisions` | Any | List decision records |
| `POST` | `/projects/{pid}/decisions` | Any | Create a decision record |
| `GET` | `/decisions/{id}` | Any | Get decision record |

#### Agents & Registration

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/agents/register` | Admin API Key | Register a new agent, returns the agent's API key |
| `GET` | `/agents` | Any | List agents in the organization |
| `GET` | `/agents/me` | Any | Get current agent identity and assignments |
| `PATCH` | `/agents/{id}` | Admin API Key | Update agent (name, platform, status) |
| `DELETE` | `/agents/{id}` | Admin API Key | Deactivate an agent, revoke its API key |

#### Notifications

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/agents/me/notifications` | Any | Poll for notifications (blocked tasks, status changes) |
| `POST` | `/agents/me/notifications/{id}/ack` | Any | Acknowledge a notification |

### 5.4 MCP Server Interface

Hiveboard also exposes an MCP (Model Context Protocol) server, enabling AI agents that support MCP to interact without custom API integration.

**MCP Tools exposed:**

| Tool Name | Maps to | Description |
|---|---|---|
| `hiveboard_list_tasks` | `GET /tasks` | List tasks with filters |
| `hiveboard_get_task` | `GET /tasks/{id}` | Get full task context |
| `hiveboard_update_status` | `PATCH /tasks/{id}/status` | Transition task status |
| `hiveboard_add_note` | `POST /tasks/{id}/notes` | Add a note to a task |
| `hiveboard_decompose_task` | `POST /tasks/{id}/subtasks` | Break down a task |
| `hiveboard_add_decision` | `POST /decisions` | Record an architectural decision |
| `hiveboard_get_dependencies` | `GET /dependencies/graph` | View dependency graph |
| `hiveboard_my_tasks` | `GET /agents/me` | See assigned tasks |
| `hiveboard_get_notifications` | `GET /notifications` | Poll notifications |

**MCP Resources exposed:**

| Resource URI | Description |
|---|---|
| `hiveboard://project/{id}/overview` | Project summary, stats, active agents |
| `hiveboard://task/{id}/context` | Full task context bundle |
| `hiveboard://project/{id}/decisions` | All decision records |

### 5.5 Key API Behaviors

#### Full Task Context Response

When an agent queries `GET /tasks/{id}`, the response includes the **full context**:

```json
{
  "task": {
    "id": "...",
    "title": "...",
    "description": "...",
    "status": "assigned",
    "metadata": {
      "branch": "feature/auth-module",
      "pr_url": null
    }
  },
  "epic": { "id": "...", "title": "..." },
  "parent_task": null,
  "subtasks": [],
  "dependencies": {
    "blocked_by": [
      { "task_id": "...", "title": "...", "status": "done" }
    ],
    "blocking": [
      { "task_id": "...", "title": "...", "status": "backlog" }
    ]
  },
  "notes": [
    {
      "agent": "claude-code-1",
      "type": "context",
      "content": "The auth module should use JWT...",
      "created_at": "..."
    }
  ],
  "events": [...],
  "related_decisions": [...]
}
```

#### Automatic Orchestrator Notification on Block

When any agent transitions a task to `blocked`:

1. A notification record is created for the orchestrator agent
2. The notification includes the `blocked_reason` and the blocking agent's identity
3. The orchestrator retrieves notifications via `GET /agents/me/notifications`

#### Dependency Enforcement

- Creating a circular dependency returns `400 Bad Request` with the detected cycle
- Transitioning a task to `in-progress` when a `blocks` dependency is not `done` returns `409 Conflict` with the unmet dependencies listed
- When a dependency task transitions to `done`, notifications are sent to agents assigned to the now-unblocked tasks

#### Task Assignment Constraint

- `POST /tasks/{id}` assign — if the task is already assigned to another agent, returns `409 Conflict`
- Only orchestrator agents can assign tasks
- Worker agents can only modify tasks assigned to them

---

## 6. Task Workflow

### 6.1 State Machine

```
                    ┌────────────┐
                    │  backlog   │
                    └─────┬──────┘
                          │ orchestrator assigns agent
                          ▼
                    ┌────────────┐
             ┌──── │  assigned   │
             │     └─────┬──────┘
             │           │ agent starts work
             │           ▼
             │     ┌────────────┐
             │     │in-progress │◄──────────┐
             │     └──┬────┬────┘           │
             │        │    │                │
             │        │    │ agent requests │ reviewer sends
             │        │    │ review         │ back
             │        │    ▼                │
             │        │  ┌────────────┐     │
             │        │  │ in-review  │─────┘
             │        │  └─────┬──────┘
             │        │        │ review passes
             │        │        ▼
             │        │  ┌────────────┐
             │        │  │   done     │
             │        │  └────────────┘
             │        │
             │        │ agent reports blocker
             │        ▼
             │  ┌────────────┐
             └─►│  blocked   │──── orchestrator resolves ───► assigned
                └────────────┘
```

### 6.2 State Transition Rules

| From | To | Who Can Trigger | Side Effects |
|---|---|---|---|
| `backlog` | `assigned` | Orchestrator | Sets `assigned_agent_id` |
| `assigned` | `in-progress` | Assigned Agent | Validates all dependencies met |
| `in-progress` | `in-review` | Assigned Agent | — |
| `in-progress` | `blocked` | Assigned Agent | Requires `blocked_reason`; notifies orchestrator |
| `in-progress` | `done` | Assigned Agent | Notifies agents waiting on this dependency |
| `in-review` | `in-progress` | Orchestrator or Reviewer | Sends back for more work |
| `in-review` | `done` | Orchestrator or Reviewer | Notifies agents waiting on this dependency |
| `blocked` | `assigned` | Orchestrator | Clears `blocked_reason` |
| Any | `backlog` | Orchestrator | Unassigns agent, resets task |

### 6.3 Task Decomposition

When an agent decomposes a task:

1. Agent calls `POST /tasks/{id}/subtasks` with an array of subtask definitions
2. The parent task status moves to `in-progress` (it will not be `done` until all subtasks are `done`)
3. Subtasks are created in `backlog` status
4. The orchestrator is notified that new subtasks are available for assignment
5. Parent task auto-transitions to `done` when all subtasks reach `done`

---

## 7. Notification System

### 7.1 Notification Types

| Type | Recipient | Trigger |
|---|---|---|
| `task_blocked` | Orchestrator | Agent marks task as blocked |
| `task_decomposed` | Orchestrator | Agent decomposes a task into subtasks |
| `dependency_resolved` | Assigned agent of dependent task | A blocking task transitions to `done` |
| `task_assigned` | Worker agent | Orchestrator assigns a task |
| `review_requested` | Orchestrator | Agent moves task to in-review |

### 7.2 Delivery Mechanism (MVP)

**Polling-based:** Agents call `GET /agents/me/notifications` to retrieve unacknowledged notifications. Notifications are persisted and returned until acknowledged.

**Post-MVP:** WebSocket / SSE push for real-time delivery.

---

## 8. Dashboard (MVP)

### 8.1 Purpose

A **read-only** web-based dashboard that gives humans visibility into what their AI agents are doing.

### 8.2 Views

| View | Description |
|---|---|
| **Project Overview** | List of projects with summary stats (active tasks, agents online, completion %) |
| **Task Board** | Kanban-style board showing tasks by status (backlog → done) |
| **Dependency Graph** | Visual graph of task dependencies with status coloring |
| **Agent Activity** | List of agents with their current task, last activity timestamp, recent events |
| **Event Timeline** | Chronological feed of all task events across the project |
| **Decision Log** | List of architectural decision records |

### 8.3 Technical Approach

- **React SPA** built with Vite, bundled as static files and served by the ASP.NET Core host
- The API project serves the built React app from `wwwroot/` at `/dashboard`
- The React app calls the REST API (`/api/v1/...`) to fetch data — no direct DB access
- No authentication for MVP (local access); API key auth for cloud version
- Auto-refreshes on a polling interval (10 seconds)
- Styling: Tailwind CSS, dark theme

---

## 9. MVP Scope

### 9.1 In Scope (MVP)

| Category | Features |
|---|---|
| **Core API** | REST API with full CRUD for projects, epics, tasks, notes, dependencies, decisions |
| **Task Workflow** | State machine with `backlog → assigned → in-progress → in-review → done → blocked` |
| **Dependencies** | Task dependency creation, circular detection, enforcement, auto-notification |
| **Task Decomposition** | Agents can break tasks into subtasks; parent auto-completes |
| **Agent Identity** | API key auth, agent registration, orchestrator/worker roles |
| **Context Store** | Full task context retrieval (notes, history, dependencies, decisions) |
| **Decision Log** | Free-form markdown decision records linked to projects/tasks |
| **Notifications** | Polling-based notifications for orchestrator and workers |
| **MCP Interface** | MCP server alongside REST for zero-config agent integration |
| **Dashboard** | Read-only web dashboard (project overview, task board, activity feed) |
| **Storage** | SQLite for local, PostgreSQL for cloud (via EF Core) |
| **Multi-tenancy** | Organization → Projects → Agents scoping |

### 9.2 Out of Scope (MVP)

| Feature | Planned For |
|---|---|
| Task priority levels | v1.1 |
| GitHub/GitLab API integration (branches, PRs) | v1.1 |
| Human intervention (reassign, approve from dashboard) | v1.1 |
| WebSocket / SSE real-time notifications | v1.1 |
| Multiple orchestrators per project | v2.0 |
| Agent performance analytics | v2.0 |
| SSO / OAuth authentication | v2.0 (cloud) |
| Audit log export | v2.0 (cloud) |
| File/code-structure awareness | Not planned |

---

## 10. Non-Functional Requirements

### 10.1 Performance

| Metric | Target |
|---|---|
| API response time (p95) | < 200ms |
| Concurrent agents per project | 20+ |
| Tasks per project | 10,000+ |
| Dependency graph resolution | < 500ms for 1,000-node graph |

### 10.2 Reliability

- Idempotent API operations where possible
- Optimistic concurrency on task updates (prevent race conditions on status transitions)
- Graceful handling of agent disconnection (stale `last_seen_at`)

### 10.3 Security

- API keys hashed with SHA-256 at rest
- Rate limiting per API key
- Input validation on all endpoints
- No secrets in logs
- CORS configuration for dashboard

### 10.4 Observability

- Structured logging (Serilog)
- Health check endpoint (`/health`)
- Metrics endpoint for agent count, task throughput, API latency

---

## 11. Success Metrics

| Metric | Target (3 months post-launch) |
|---|---|
| GitHub stars (open-source) | 500+ |
| Self-hosted installations | 200+ |
| Agents connected (across all installations) | 1,000+ |
| Tasks completed through the system | 10,000+ |
| Community contributions (PRs) | 20+ |

---

## 12. Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Agents don't adopt API keys workflow | Low adoption | MCP interface makes integration near-zero-effort |
| Orchestrator agent is a single point of failure | Project stalls if orchestrator fails | Dashboard shows orchestrator health; manual API fallback |
| Dependency graph becomes complex | Performance degradation | Indexed graph queries; limit depth; async resolution |
| Agent platforms change their tool-use protocols | Integration breaks | Abstraction layer; MCP as stable interface |
| Security of agent API keys in agent prompts | Key exposure | Short-lived keys, key rotation API, scoped permissions |

---

## 13. Future Vision (Post-MVP)

1. **Agent Marketplace** — pre-built orchestrator templates for common workflows (feature dev, bug triage, migration)
2. **Smart Dependency Suggestions** — system suggests dependencies based on task descriptions
3. **Cross-Project Context** — agents can reference decisions from other projects
4. **Agent Performance Scoring** — track completion rate, quality (rework ratio), speed
5. **Human-in-the-loop** — approval gates, review steps, override capabilities from dashboard
6. **Native Git Integration** — auto-create branches, link PRs, verify CI status
7. **Event Webhooks** — push events to external systems (Slack, Discord, custom)

---

## Appendix A: Agent Integration Examples

### A.1 Orchestrator Agent Workflow

```
1. Orchestrator receives a high-level goal from the human
2. POST /projects → creates project
3. POST /projects/{id}/epics → breaks goal into epics
4. POST /projects/{id}/tasks → creates tasks within epics
5. POST /tasks/{id}/dependencies → wires up task dependencies
6. PATCH /tasks/{id} → assigns ready tasks to available worker agents
7. GET /agents/me/notifications → polls for blocked tasks / completed tasks
8. Repeat: assign new tasks as dependencies clear
```

### A.2 Worker Agent Workflow

```
1. GET /agents/me/notifications → sees new task assignment
2. GET /tasks/{id} → retrieves full context (description, notes, dependencies, decisions)
3. PATCH /tasks/{id}/status → moves to "in-progress"
4. [does the work]
5. POST /tasks/{id}/notes → shares context, progress, or decisions
6. POST /projects/{pid}/decisions → records architectural decision
7. PATCH /tasks/{id}/status → moves to "in-review" or "done"
8. PATCH /tasks/{id} → updates metadata (branch name, PR URL)
```

### A.3 Decomposition Workflow

```
1. Worker agent receives complex task
2. Agent analyzes the task and determines subtasks needed
3. POST /tasks/{id}/subtasks → [{ title, description }, ...]
4. System creates subtasks in "backlog", notifies orchestrator
5. Orchestrator assigns subtasks to available agents
6. When all subtasks are "done", parent task auto-completes
```
