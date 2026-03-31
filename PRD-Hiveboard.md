# Hiveboard - Product Requirements Document

## Headless Coordination for AI Coding Agents

**Version:** 1.0 (MVP)
**Date:** March 13, 2026
**Status:** Draft

---

## 1. Overview

### 1.1 Product Vision

Hiveboard is a headless, API-first coordination system purpose-built for AI coding agents and the humans directing them. Unlike traditional PM tools designed around human-only interaction, Hiveboard is optimized for human-supervised, machine-to-machine collaboration — enabling multiple AI agents to collaborate on software projects through structured task management, dependency tracking, shared context, and explicit control points for human operators.

### 1.2 Problem Statement

As AI coding agents (GitHub Copilot, Claude Code, Cursor, OpenAI Codex) become integral to development workflows, teams increasingly run multiple agents concurrently on the same project. There is no purpose-built system for:

- **Sequencing dependent tasks** across multiple agents working in parallel
- **Sharing context and decisions** so agents don't duplicate work or make contradictory choices
- **Coordinating work** to prevent conflicts and ensure coherent output
- **Providing direct control and visibility** to humans overseeing agent-driven development

Today, developers cobble together GitHub Issues, markdown files, manual prompts, and ad hoc review loops to coordinate agents.
Hiveboard replaces this with a structured, agent-native system where humans stay in control and agents benefit from a durable shared operating model.

### 1.3 Product Name

**Hiveboard** - hive mind + board, emphasizing multi-agent collaboration.

### 1.4 Business Model

**Open-core:**

- **Free (open-source core):** Self-hosted, single-organization, SQLite storage, REST + MCP API, human control dashboard
- **Paid (cloud/enterprise):** Cloud-hosted, multi-organization, PostgreSQL, advanced analytics, audit logs, SSO, priority support

---

## 2. Target Users

### 2.1 Primary Personas

| Persona | Description | Key Need |
|---|---|---|
| **Solo Developer** | Runs 2–5 AI agents on personal/side projects | Coordinate agents without babysitting; see what's happening |
| **Dev Team** | 3–15 person team using agents for feature work | Prevent agent conflicts; maintain architectural coherence |
| **Human Operator** | Technical lead or developer coordinating work through the dashboard/API | Stay in control; intervene quickly; understand what is happening |
| **Orchestrator Agent** | An optional AI agent (external) that suggests or automates coordination steps | Propose decomposition, dependencies, and assignments |
| **Worker Agent** | An AI agent that executes assigned tasks | Retrieve task details, report progress, share context |
### 2.2 Non-Users (Explicitly Out of Scope)

- Human-only project managers using this as a generic PM suite
- Non-coding workflows (marketing, design, etc.)

---

## 3. Architecture

### 3.1 Deployment Models

| Model | Storage | Use Case |
|---|---|---|
| **Self-hosted (local)** | SQLite | Solo developers, trusted local coordination workflows |
| **Cloud-hosted** | PostgreSQL | Teams, persistent projects, shared human + agent access |

### 3.2 System Architecture

`
Human Operator / Optional Orchestrator Agent
                    |
                    v
        +------------------------------+
        |        Hiveboard API         |
        |   REST + MCP coordination    |
        +--------------+---------------+
                       |
        +--------------+---------------+
        |              |               |
        v              v               v
  Dashboard UI     Worker Agent    Worker Agent
   (control plane)   Client 1        Client 2
                       |
                       v
          SQLite / PostgreSQL persistence
`

### 3.3 Multi-Tenancy

- **Organization** is the top-level tenant
- Each organization has multiple **Projects**
- Each project can have zero or one **Orchestrator Agent** and multiple **Worker Agents**
- Human operators coordinate work through the dashboard/API in MVP
- Agent API keys are scoped to an organization

### 3.4 Technology Stack

| Component | Technology |
|---|---|
| Language | C# / .NET 10 |
| API Framework | ASP.NET Core Minimal API |
| Database (local) | SQLite via EF Core |
| Database (cloud) | PostgreSQL via EF Core |
| MCP Server | .NET MCP SDK |
| Dashboard | React SPA (standalone app, optionally bundled by the API host for self-hosted deployments) |
| Authentication | API key-based |
| Serialization | System.Text.Json |

---

## 4. Data Model

### 4.1 Entity Relationship

```
Organization (1) ---- (*) Project
Project (1) ---- (*) Epic
Project (1) ---- (*) Task
Project (1) ---- (*) DecisionRecord
Project (1) ———— (0..1) OrchestratorAgent
Project (1) ---- (*) WorkerAgent

Epic (1) ---- (*) Task
Task (1) ---- (*) Task (subtasks - decomposition)
Task (1) ---- (*) TaskDependency
Task (1) ---- (*) TaskNote
Task (1) ---- (*) TaskEvent (audit trail)
Task (1) ---- (0..1) Agent (assigned)
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
- Only a **coordinator** can assign tasks (a human operator in MVP, optionally an orchestrator agent)
- When status changes to `blocked`, the system fires a notification to the coordinator

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

All agent API requests require an `X-Api-Key` header. The API key identifies both the agent and the organization. The system determines agent role (orchestrator vs. worker) from the key. In self-hosted MVP, human coordination happens through the dashboard/control plane using a session-scoped coordinator/admin credential.

```
X-Api-Key: hb_sk_abc123...
```

### 5.2 Agent Registration & Onboarding

Before an agent can interact with Hiveboard, it must be registered. Registration is an **admin operation** - performed by a human or setup script, not by the agents themselves.

#### Registration Flow

```
1. Human/setup script calls POST /api/v1/agents/register with an Admin API Key
   Body: { "name": "claude-worker-1", "type": "worker", "platform": "claude-code", "organizationId": "..." }

2. Hiveboard generates a unique API key (hb_sk_...), hashes it with SHA-256, and stores the hash

3. The response returns the plaintext API key ONCE - it is never stored or retrievable again:
   { "agentId": "...", "apiKey": "hb_sk_abc123...", "name": "claude-worker-1", "type": "worker" }

4. The human configures the agent with this API key:
   - For MCP agents: set HIVEBOARD_API_KEY in the MCP server config
   - For REST agents: include the key in X-Api-Key header

5. The agent calls GET /api/v1/agents/me to verify its identity and see assigned tasks

6. The agent is now visible to coordinators and can receive task assignments
```

#### Admin API Key

A special **Admin API Key** is generated on first startup and printed to the console / log. This key is used for agent management operations (register, update, deactivate, rotate keys) and for authenticated control-plane actions in the self-hosted MVP. It is not tied to any agent — it represents the human administrator.

The server stores only a **SHA-256 hash** of the admin key and a visible **key prefix** (first 12 characters, e.g., `hb_adm_ab12cd`) for identification in the dashboard. The plaintext key is never stored.

The admin key can also be set via environment variable `HIVEBOARD_ADMIN_KEY` for automated deployments.

#### Admin Key Rotation

The admin key can be rotated at any time via the API or the dashboard Admin Panel:

```
1. Human calls POST /admin/keys/rotate with the current Admin API Key
2. Server generates a new key (hb_adm_...), hashes it, stores the hash and prefix
3. The new plaintext key is returned ONCE in the response — store it immediately
4. The old key is immediately invalidated — any request using it returns 401
```

**Recovery:** If the admin key is lost, set `HIVEBOARD_ADMIN_KEY` in the environment and restart the server. The env-supplied value overrides the stored hash and becomes the active key.

#### Agent Key Rotation

Individual agent keys can be rotated without deactivating and re-registering the agent (e.g., when a key is compromised):

```
1. Human calls POST /agents/{id}/keys/rotate with the Admin API Key
2. Server generates a new agent key (hb_sk_...), hashes it, replaces the stored hash
3. New plaintext key is returned ONCE — reconfigure the agent with this key
4. The old agent key is immediately invalidated
```

#### Agent Lifecycle

| State | Description |
|---|---|
| **Registered** | Agent is created, API key issued, but hasn't made any API call yet |
| **Active** | Agent has made at least one API call (`last_seen_at` is set) |
| **Inactive** | Agent has been deactivated by admin — API key is revoked, all calls return 401 |

#### Orchestrator Assignment to Project

After registration, an orchestrator agent can be linked to a project when it creates or is assigned to one. Projects remain operable without an orchestrator agent because a human coordinator can manage them directly. When an orchestrator creates a project via `POST /projects`, it automatically becomes that project's orchestrator.

#### Worker Availability

Once registered, worker agents become available to coordinators. A coordinator discovers available workers by calling `GET /agents` (filtered to workers in their organization). Workers don't need to explicitly "join" a project — the coordinator assigns them tasks, which implicitly links them.

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
| `POST` | `/agents/{id}/keys/rotate` | Admin API Key | Rotate an agent's API key; old key immediately invalidated; new plaintext returned once |

#### Admin Key Management

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/admin/keys/info` | Admin API Key | Get admin key metadata (prefix, `created_at`, `last_used_at`) |
| `POST` | `/admin/keys/rotate` | Admin API Key | Rotate the admin key; old key immediately invalidated; new plaintext returned once |

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

- `POST /tasks/{id}` assign - if the task is already assigned to another agent, returns `409 Conflict`
- Only orchestrator agents can assign tasks
- Worker agents can only modify tasks assigned to them

### 5.6 API Discoverability & Documentation

To support both human operators and agent toolchains, the REST API must expose machine-readable and interactive documentation.

- OpenAPI 3.x specification is available at `/openapi/v1.json` (or equivalent `/swagger/v1/swagger.json`)
- Interactive API explorer is available at `/swagger` for development and integration testing
- OpenAPI security scheme documents `X-Api-Key` authentication and role expectations (`Any`, `Orchestrator`, `Admin API Key`)
- Every REST endpoint defines summary, description, request schema, response schemas, and expected status codes
- Core workflow endpoints include representative examples (`GET /tasks/{id}`, `PATCH /tasks/{id}/status`, `POST /tasks/{id}/subtasks`)
- Operation IDs and tags are stable across patch releases to avoid breaking generated clients and agent integrations

### 5.7 MCP Discoverability & Contract Stability

MCP has built-in discovery, but Hiveboard still needs a stable, well-documented MCP contract for reliable agent interoperability.

- MCP discovery must expose complete tool/resource metadata via standard MCP discovery operations (for example `list_tools` and `list_resources`)
- Every MCP tool includes: stable name, clear description, input schema, and structured output shape (when supported by the SDK)
- Every MCP resource includes: stable URI pattern, description, and response shape expectations
- Tool/resource names are stable across patch releases; breaking changes require explicit versioning strategy (new tool/resource names or versioned server contract)
- Validation/auth/not-found/conflict cases return structured MCP errors with machine-readable codes and actionable messages
- A working MCP client configuration example is maintained for local integration testing

---

## 6. Task Workflow

### 6.1 State Machine

```
                    +------------+
                    |  backlog    |
                    +-----+------+
                          |
                          | coordinator assigns agent
                          v
                    +------------+
             +----> |  assigned   |
             |      +-----+------+
             |            |
             |            | agent starts work
             |            v
             |      +------------+
             |      | in-progress | <----------------+
             |      +--+------+---+                  |
             |         |      |                      |
             |         |      | agent requests       | coordinator sends back
             |         |      | review               |
             |         |      v                      |
             |         |  +------------+             |
             |         |  | in-review  | ------------+
             |         |  +-----+------+
             |         |        |
             |         |        | review passes
             |         |        v
             |         |  +------------+
             |         |  |    done    |
             |         |  +------------+
             |         |
             |         | agent reports blocker
             |         v
             +------> +------------+ ---- coordinator resolves ---> assigned
                      |  blocked    |
                      +------------+
```

### 6.2 State Transition Rules

| From | To | Who Can Trigger | Side Effects |
|---|---|---|---|
| `backlog` | `assigned` | Coordinator | Sets `assigned_agent_id` |
| `assigned` | `in-progress` | Assigned Agent | Validates all dependencies met |
| `in-progress` | `in-review` | Assigned Agent | Requests coordinator review |
| `in-progress` | `blocked` | Assigned Agent | Requires `blocked_reason`; notifies coordinator |
| `in-progress` | `done` | Assigned Agent | Notifies agents waiting on this dependency |
| `in-review` | `in-progress` | Coordinator or Reviewer | Sends back for more work |
| `in-review` | `done` | Coordinator or Reviewer | Notifies agents waiting on this dependency |
| `blocked` | `assigned` | Coordinator | Clears `blocked_reason` |
| Any | `backlog` | Coordinator | Unassigns agent, resets task |

### 6.3 Task Decomposition

When an agent decomposes a task:

1. Agent calls POST /tasks/{id}/subtasks with an array of subtask definitions
2. The parent task status moves to in-progress (it will not be done until all subtasks are done)
3. Subtasks are created in acklog status
4. The coordinator is notified that new subtasks are available for assignment
5. Parent task auto-transitions to done when all subtasks reach done

---

## 7. Notification System

### 7.1 Notification Types

| Type | Recipient | Trigger |
|---|---|---|
| 	ask_blocked | Coordinator | Agent marks task as blocked |
| 	ask_decomposed | Coordinator | Agent decomposes a task into subtasks |
| dependency_resolved | Assigned agent of dependent task | A blocking task transitions to done |
| 	ask_assigned | Worker agent | Coordinator assigns a task |
| 
eview_requested | Coordinator | Agent moves task to in-review |

### 7.2 Delivery Mechanism (MVP)

**Polling-based:** Agents call GET /agents/me/notifications to retrieve unacknowledged notifications. Notifications are persisted and returned until acknowledged.

**Post-MVP:** WebSocket / SSE push for real-time delivery.

---

## 8. Dashboard (MVP)

### 8.1 Purpose

A web-based dashboard that gives humans visibility into what their AI agents are doing and serves as the primary coordination surface for the MVP. It combines observability with core intervention flows such as task creation, assignment, review approval, and blocker resolution.

### 8.2 Views

| View | Description |
|---|---|
| **Project Overview** | List of projects with summary stats (active tasks, agents online, completion %) |
| **Task Board** | Kanban-style board showing tasks by status (backlog → done), with create/edit/assign controls |
| **Dependency Graph** | Visual graph of task dependencies with status coloring |
| **Agent Activity** | List of agents with their current task, last activity timestamp, recent events |
| **Event Timeline** | Chronological feed of all task events across the project |
| **Decision Log** | List of architectural decision records |
| **Coordinator Console** | Resolve blockers, approve/send back review, reassign work, and inspect pending suggestions |
| **Admin Panel** | Key management: admin key metadata (prefix, created/last-used timestamps), rotate admin key, view and rotate individual agent keys |

### 8.3 Technical Approach

- **React SPA** built with Vite as a standalone frontend app in the monorepo
- In development and cloud deployments, the dashboard runs independently from the API
- For self-hosted packaging, the built dashboard may be bundled and served by the ASP.NET Core host at `/dashboard`
- The React app calls the REST API (`/api/v1/...`) to fetch data — no direct DB access
- The dashboard prompts for the coordinator/admin credential once per browser session — it is stored in **session storage only** (never persisted to localStorage or cookies) and sent as the `X-Api-Key` header for privileged requests
- Auto-refreshes on a polling interval (10 seconds)
- Styling: Tailwind CSS, dark theme

---

## 9. MVP Scope

### 9.1 In Scope (MVP)

| Category | Features |
|---|---|
| **Core API** | REST API with full CRUD for projects, epics, tasks, notes, dependencies, decisions |
| **Task Workflow** | State machine with `backlog → assigned → in-progress → in-review → done → blocked` and coordinator intervention points |
| **Dependencies** | Task dependency creation, circular detection, enforcement, auto-notification |
| **Task Decomposition** | Agents can break tasks into subtasks; parent auto-completes |
| **Agent Identity** | API key auth, agent registration, orchestrator/worker roles |
| **Human Coordination** | Human operators create tasks, assign agents, approve review, and resolve blockers |
| **Admin Key Management** | Admin key rotation, agent key rotation, key metadata (prefix, created/last-used timestamps) |
| **Context Store** | Full task context retrieval (notes, history, dependencies, decisions) |
| **Decision Log** | Free-form markdown decision records linked to projects/tasks |
| **Notifications** | Polling-based notifications for coordinators and workers |
| **MCP Interface** | MCP server alongside REST for zero-config agent integration and optional co-orchestration |
| **Dashboard** | Human control plane for project overview, task board, activity feed, review, and blocker resolution |
| **Storage** | SQLite for local, PostgreSQL for cloud (via EF Core) |
| **Multi-tenancy** | Organization → Projects → Agents scoping |

### 9.2 Out of Scope (MVP)

| Feature | Planned For |
|---|---|
| Task priority levels | v1.1 |
| GitHub/GitLab API integration (branches, PRs) | v1.1 |
| Fully autonomous orchestration (auto-apply suggestions without human approval) | v1.1 |
| WebSocket / SSE real-time notifications | v1.1 |
| Multiple orchestrators per project | v2.0 |
| Agent performance analytics | v2.0 |
| SSO / OAuth authentication | v2.0 (cloud) |

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
- OpenAPI spec generation is validated in CI to prevent documentation drift from implemented endpoints
- MCP discovery and tool/resource schema contracts are validated in integration tests to prevent protocol drift

### 10.3 Security

- API keys hashed with SHA-256 at rest; only key prefix stored for human identification
- Admin key and agent key rotation available at any time via API and dashboard Admin Panel
- Admin key `last_used_at` tracked to detect unauthorized use
- Admin key session-stored in dashboard (session storage, never localStorage)
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
| REST endpoints documented in OpenAPI | 100% |

---

## 12. Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Humans can't tell whether a failure is Hiveboard or a bad agent plan | Trust erosion | Keep human approval in MVP; surface agent suggestions and task history clearly |
| Dashboard control flow is weak or unclear | Users fall back to ad hoc tools | Prioritize create/assign/review/blocker workflows before autonomous orchestration |
| Dependency graph becomes complex | Performance degradation | Indexed graph queries; limit depth; async resolution |
| Agent platforms change their tool-use protocols | Integration breaks | Abstraction layer; MCP as stable interface |
| Security of agent API keys in agent prompts | Key exposure | Short-lived keys, key rotation API, scoped permissions |

---

## 13. Future Vision (Post-MVP)

1. **Agent Marketplace** — pre-built co-orchestrator templates for common workflows (feature dev, bug triage, migration)
2. **Smart Dependency Suggestions** — system suggests dependencies based on task descriptions
3. **Cross-Project Context** — agents can reference decisions from other projects
4. **Agent Performance Scoring** — track completion rate, quality (rework ratio), speed
5. **Autonomous Co-Orchestration** — agents can optionally auto-apply approved planning patterns
6. **Native Git Integration** — auto-create branches, link PRs, verify CI status
7. **Event Webhooks** — push events to external systems (Slack, Discord, custom)

---

## Appendix A: Agent Integration Examples

### A.1 Human Coordinator Workflow

```
1. Human coordinator receives a high-level goal
2. POST /projects → creates project
3. POST /projects/{id}/epics → breaks goal into epics
4. POST /projects/{id}/tasks → creates tasks within epics
5. POST /tasks/{id}/dependencies → wires up task dependencies
6. PATCH /tasks/{id} → assigns ready tasks to available worker agents
7. Reviews activity, blockers, and pending approvals in the dashboard/control plane
8. Repeats: resolve blockers, approve review, and assign new tasks as dependencies clear
```

### A.2 Worker Agent Workflow

```
1. GET /agents/me/notifications â†’ sees new task assignment
2. GET /tasks/{id} â†’ retrieves full context (description, notes, dependencies, decisions)
3. PATCH /tasks/{id}/status â†’ moves to "in-progress"
4. [does the work]
5. POST /tasks/{id}/notes â†’ shares context, progress, or decisions
6. POST /projects/{pid}/decisions â†’ records architectural decision
7. PATCH /tasks/{id}/status â†’ moves to "in-review" or "done"
8. PATCH /tasks/{id} â†’ updates metadata (branch name, PR URL)
```

### A.3 Decomposition Workflow

```
1. Worker agent receives complex task
2. Agent analyzes the task and determines subtasks needed
3. POST /tasks/{id}/subtasks â†’ [{ title, description }, ...]
4. System creates subtasks in "backlog", notifies the coordinator
5. Coordinator assigns subtasks to available agents
6. When all subtasks are "done", parent task auto-completes
```


