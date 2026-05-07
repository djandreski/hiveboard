# Hiveboard — Implementation Guide

## How to Use This Guide

This guide breaks the Hiveboard PRD into **19 sequential tasks**, each sized for a single AI agent session. Work through them in order — each step builds on the previous.

**Workflow for each task:**
1. Read the task description and acceptance criteria below
2. Copy the **Agent Prompt** into your AI coding agent
3. Verify the **Acceptance Criteria** before moving to the next task
4. Commit after each task passes

**Important:** Always provide the PRD (`PRD-Hiveboard.md`) as context alongside the agent prompt. The PRD is the source of truth for data model, API design, and business rules.

**Direction Update (after Task 8):**
- Tasks 1–8 were implemented under the original orchestrator-first framing and remain valid as historical implementation steps.
- Task 9 realigns the current codebase to the coordinator-first MVP while keeping optional co-orchestrator support.
- From Task 10 onward, assume the coordinator-first control model is already in place.
- `src/Hiveboard.Dashboard` is developed as a standalone frontend app in the monorepo. Optional bundling into the API host is a deployment concern, not the default development model.

### Understanding File Targets

Each task lists **File Targets** — the files involved in that task.

- **Files that don't exist yet** (most early tasks): These are the files the agent should **create**. They define the expected output structure. Include them in your prompt so the agent knows what to produce and where to put it.
- **Files marked "Update"** (e.g., `Update src/Hiveboard.Api/Program.cs`): These are existing files from previous tasks that need modification. Point the agent to these files so it can read and edit them.
- **Files marked "(generated)"**: These are created by tooling (e.g., EF Core migrations), not hand-written. The agent should run the commands that produce them.

When starting a fresh task, provide:
1. The **Agent Prompt** (copy-paste from below)
2. The **PRD** as context
3. The **File Targets list** — tell the agent "these are the files you need to create/modify"
4. Any **existing files from prior tasks** that the new task depends on (the agent needs to read them for context)

---

## Phase 1: Foundation

### Task 1 — Solution Structure & Project Scaffolding
**Status:** Implemented

**Goal:** Create the .NET solution with properly separated projects, NuGet packages, and project references.

**File Targets:**
Create these files/projects (none exist yet — the agent creates them all):
- `Hiveboard.sln` (new solution)
- `src/Hiveboard.Api/Hiveboard.Api.csproj`
- `src/Hiveboard.Core/Hiveboard.Core.csproj`
- `src/Hiveboard.Infrastructure/Hiveboard.Infrastructure.csproj`
- `src/Hiveboard.Dashboard/` (React SPA — initialized with Vite)
- `tests/Hiveboard.Tests/Hiveboard.Tests.csproj`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 1 status to Implementing.
- At the END (after acceptance criteria pass), set Task 1 status to Implemented and Task 2 status to Implementing.

Create a new .NET 10 solution called "Hiveboard" with clean architecture.
The solution should have these projects:

1. src/Hiveboard.Api — ASP.NET Core Minimal API project. This is the host.
   NuGet: Microsoft.AspNetCore.OpenApi, Swashbuckle.AspNetCore
   References: Hiveboard.Core, Hiveboard.Infrastructure

2. src/Hiveboard.Core — Class library. Domain entities, interfaces, enums, no dependencies on infrastructure.
   No external NuGet packages.

3. src/Hiveboard.Infrastructure — Class library. EF Core, database, repositories.
   NuGet: Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Sqlite,
          Npgsql.EntityFrameworkCore.PostgreSQL,
          Microsoft.EntityFrameworkCore.Design
   References: Hiveboard.Core

4. src/Hiveboard.Dashboard — React SPA project (Vite + React + TypeScript + Tailwind CSS).
   This is NOT a .NET project. Initialize with: npm create vite@latest Hiveboard.Dashboard -- --template react-ts
   Install Tailwind CSS. The built output will be copied to Hiveboard.Api/wwwroot/ at build time.

5. tests/Hiveboard.Tests — xUnit test project.
   NuGet: xunit, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio,
          Microsoft.AspNetCore.Mvc.Testing, Microsoft.EntityFrameworkCore.InMemory
   References: Hiveboard.Api, Hiveboard.Core, Hiveboard.Infrastructure

Create a Hiveboard.sln file at the root that includes all projects with solution folders (src/, tests/).

Each project should target net10.0. The Api project should have a minimal Program.cs that starts and runs (just builder + app.Run, no endpoints yet). Core and Infrastructure should have placeholder README.md files.

Do NOT create the existing ClaudeAgents projects — this is a new, separate solution.
```

**Acceptance Criteria:**
- [ ] `dotnet build Hiveboard.sln` succeeds with no errors
- [ ] `dotnet test` runs (0 tests, but no errors)
- [ ] API project starts and responds on a port
- [ ] Project references are correct (Api → Core + Infra, Infra → Core)
- [ ] No circular references

---

### Task 2 — Domain Entities & Enums
**Status:** Implemented

**Goal:** Create all domain entities and enums in the Core project, matching the PRD data model exactly.

**File Targets:**
Create these files (none exist yet):
- `src/Hiveboard.Core/Entities/Organization.cs`
- `src/Hiveboard.Core/Entities/Project.cs`
- `src/Hiveboard.Core/Entities/Agent.cs`
- `src/Hiveboard.Core/Entities/Epic.cs`
- `src/Hiveboard.Core/Entities/AgentTask.cs`
- `src/Hiveboard.Core/Entities/TaskDependency.cs`
- `src/Hiveboard.Core/Entities/TaskNote.cs`
- `src/Hiveboard.Core/Entities/TaskEvent.cs`
- `src/Hiveboard.Core/Entities/DecisionRecord.cs`
- `src/Hiveboard.Core/Entities/Notification.cs`
- `src/Hiveboard.Core/Enums/AgentType.cs`
- `src/Hiveboard.Core/Enums/AgentPlatform.cs`
- `src/Hiveboard.Core/Enums/AgentStatus.cs`
- `src/Hiveboard.Core/Enums/ProjectStatus.cs`
- `src/Hiveboard.Core/Enums/EpicStatus.cs`
- `src/Hiveboard.Core/Enums/TaskStatus.cs`
- `src/Hiveboard.Core/Enums/NoteType.cs`
- `src/Hiveboard.Core/Enums/DecisionStatus.cs`
- `src/Hiveboard.Core/Enums/NotificationType.cs`
- `src/Hiveboard.Core/Enums/DependencyType.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 2 status to Implementing.
- At the END (after acceptance criteria pass), set Task 2 status to Implemented and Task 3 status to Implementing.

Read PRD-Hiveboard.md section 4 (Data Model) carefully.

Create all domain entities in src/Hiveboard.Core/Entities/ and all enums in src/Hiveboard.Core/Enums/.

Requirements:
- Use Guid for all Id properties
- Use DateTimeOffset for all timestamps
- Use the exact field names from the PRD, but in C# PascalCase (e.g., created_at → CreatedAt)
- Name the task entity "AgentTask" (not "Task" — conflicts with System.Threading.Tasks.Task)
- Include navigation properties for relationships (e.g., AgentTask has Epic, AssignedAgent, ParentTask, Subtasks collection, Dependencies, Notes, Events)
- The Metadata field on AgentTask should be Dictionary<string, string> (serialized as JSON)
- Enums should use the exact values from the PRD
  - TaskStatus: Backlog, Assigned, InProgress, InReview, Done, Blocked
  - AgentType: Orchestrator, Worker
  - AgentPlatform: Copilot, ClaudeCode, Cursor, Codex, Custom
  - NoteType: Context, Progress, ReviewRequest, Blocker, Resolution
  - DecisionStatus: Proposed, Accepted, Superseded
  - NotificationType: TaskBlocked, TaskDecomposed, DependencyResolved, TaskAssigned, ReviewRequested
  - DependencyType: Blocks, RequiredBy

- Add a Notification entity (not in PRD but needed for the notification system):
  - Id (Guid), AgentId, Type (NotificationType), TaskId, Message (string),
    IsAcknowledged (bool), CreatedAt (DateTimeOffset)

- All entities should be plain C# classes with public getters/setters (POCOs)
- No data annotations — we'll configure EF Core with Fluent API separately
- No business logic in entities
```

**Acceptance Criteria:**
- [ ] All entities match the PRD data model fields
- [ ] All enums have the correct values
- [ ] Navigation properties are present for all relationships
- [ ] `dotnet build` succeeds
- [ ] No dependencies on EF Core or any external package in the Core project

---

### Task 3 — EF Core DbContext & Configuration
**Status:** Implemented

**Goal:** Create the EF Core DbContext with Fluent API configuration for all entities, plus SQLite/PostgreSQL provider support.

**File Targets:**
Create these files:
- `src/Hiveboard.Infrastructure/Data/HiveboardDbContext.cs`
- `src/Hiveboard.Infrastructure/Data/Configurations/OrganizationConfiguration.cs`
- `src/Hiveboard.Infrastructure/Data/Configurations/ProjectConfiguration.cs`
- `src/Hiveboard.Infrastructure/Data/Configurations/AgentConfiguration.cs`
- `src/Hiveboard.Infrastructure/Data/Configurations/EpicConfiguration.cs`
- `src/Hiveboard.Infrastructure/Data/Configurations/AgentTaskConfiguration.cs`
- `src/Hiveboard.Infrastructure/Data/Configurations/TaskDependencyConfiguration.cs`
- `src/Hiveboard.Infrastructure/Data/Configurations/TaskNoteConfiguration.cs`
- `src/Hiveboard.Infrastructure/Data/Configurations/TaskEventConfiguration.cs`
- `src/Hiveboard.Infrastructure/Data/Configurations/DecisionRecordConfiguration.cs`
- `src/Hiveboard.Infrastructure/Data/Configurations/NotificationConfiguration.cs`
- `src/Hiveboard.Infrastructure/ServiceRegistration.cs`

Update existing (created in previous tasks):
- `src/Hiveboard.Api/Program.cs`
- `src/Hiveboard.Api/appsettings.json`

Provide as context (agent needs to read these):
- `src/Hiveboard.Core/Entities/*.cs` (all entity files from Task 2)

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 3 status to Implementing.
- At the END (after acceptance criteria pass), set Task 3 status to Implemented and Task 4 status to Implementing.

Read PRD-Hiveboard.md section 4 (Data Model) and the entity files in src/Hiveboard.Core/Entities/.

Create the EF Core setup in src/Hiveboard.Infrastructure/:

1. HiveboardDbContext with DbSet<T> for every entity. Override OnModelCreating to apply all configurations from the assembly.

2. One IEntityTypeConfiguration<T> file per entity in Data/Configurations/:
   - Configure primary keys, required fields, max lengths
   - Configure relationships with proper cascade behavior:
     - Organization → Projects: cascade delete
     - Project → Epics, Tasks, Decisions: cascade delete
     - Epic → Tasks: set null on delete (tasks can exist without epic)
     - AgentTask → Subtasks: restrict (prevent accidental cascade)
     - AgentTask → Notes, Events: cascade delete
     - AgentTask → Dependencies: cascade delete
   - Configure the Metadata property on AgentTask as a JSON column
     (use HasConversion for Dictionary<string, string> ↔ JSON string)
   - Configure indexes:
     - AgentTask: index on (ProjectId, Status), (AssignedAgentId), (EpicId), (ParentTaskId)
     - Agent: unique index on (ApiKeyHash)
     - Notification: index on (AgentId, IsAcknowledged)
     - TaskDependency: unique index on (TaskId, DependsOnTaskId)

3. ServiceRegistration.cs with an extension method:
   AddHiveboardInfrastructure(this IServiceCollection services, IConfiguration config)
   - Read "DatabaseProvider" from config ("sqlite" or "postgresql")
   - Read "ConnectionStrings:DefaultConnection" from config
   - Register the DbContext with the appropriate provider
   - If no provider specified, default to SQLite with "Data Source=hiveboard.db"

4. Wire this up in the API's Program.cs:
   builder.Services.AddHiveboardInfrastructure(builder.Configuration);
   Add app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

5. Add appsettings.json to the API project with:
   {
     "DatabaseProvider": "sqlite",
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=hiveboard.db"
     }
   }
```

**Acceptance Criteria:**
- [ ] `dotnet build` succeeds
- [ ] API starts without errors
- [ ] `/health` returns `{"status":"healthy"}`
- [ ] DbContext can be resolved from DI
- [ ] All entity configurations are applied (check with a unit test or manual review)


---

### Task 4 — Database Migrations & Seed Data
**Status:** Implemented

**Goal:** Create the initial EF Core migration and a seed data mechanism for development.

**File Targets:**
Create these files:
- `src/Hiveboard.Infrastructure/Data/HiveboardDbSeeder.cs`

Generated by tooling (agent runs the migration command):
- `src/Hiveboard.Infrastructure/Data/Migrations/` (EF Core generates these)

Update existing:
- `src/Hiveboard.Api/Program.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 4 status to Implementing.
- At the END (after acceptance criteria pass), set Task 4 status to Implemented and Task 5 status to Implementing.

Set up EF Core migrations and seed data for the Hiveboard project.

1. Add the EF Core Design package to Hiveboard.Infrastructure if not already present.

2. Create the initial migration. Run from the solution root:
   dotnet ef migrations add InitialCreate --project src/Hiveboard.Infrastructure --startup-project src/Hiveboard.Api

3. Create HiveboardDbSeeder.cs in src/Hiveboard.Infrastructure/Data/:
   - A static class with method SeedDevelopmentData(HiveboardDbContext context)
   - Creates a default Organization ("Default Org")
   - Creates a sample Project ("Sample Project")
   - Creates two Agents:
     - An orchestrator agent (name: "Orchestrator", platform: Custom, with a known dev API key "dev-orchestrator-key-123")
     - A worker agent (name: "Worker-1", platform: ClaudeCode, with a known dev API key "dev-worker-key-456")
   - Store the API key hashes using SHA256 (System.Security.Cryptography.SHA256.HashData)
   - Only seed if the database is empty (check if any Organization exists)

4. Update Program.cs in the API project:
   - After building the app but before app.Run(), add:
     using var scope = app.Services.CreateScope();
     var db = scope.ServiceProvider.GetRequiredService<HiveboardDbContext>();
     db.Database.Migrate();
   - In development environment only, call the seeder:
     if (app.Environment.IsDevelopment())
         HiveboardDbSeeder.SeedDevelopmentData(db);

Make sure the migration compiles and applies cleanly to a fresh SQLite database.
```

**Acceptance Criteria:**
- [ ] Migration files are generated and compile
- [ ] API starts and creates `hiveboard.db` automatically
- [ ] Database has all tables with correct schema
- [ ] Seed data is present (1 org, 1 project, 2 agents)
- [ ] Restarting the API doesn't duplicate seed data

---

## Phase 2: Core API

### Task 5 — Authentication & Agent Registration
**Status:** Implemented

**Goal:** Implement API key authentication, admin key bootstrap, agent registration, and key rotation endpoints.

**File Targets:**
Create these files:
- `src/Hiveboard.Api/Auth/ApiKeyAuthHandler.cs`
- `src/Hiveboard.Api/Auth/AgentContext.cs`
- `src/Hiveboard.Api/Auth/AdminKeyProvider.cs`
- `src/Hiveboard.Api/Endpoints/AgentEndpoints.cs`
- `src/Hiveboard.Api/Endpoints/AdminKeyEndpoints.cs`
- `src/Hiveboard.Api/Contracts/RegisterAgentRequest.cs`
- `src/Hiveboard.Api/Contracts/RegisterAgentResponse.cs`
- `src/Hiveboard.Api/Contracts/KeyRotationResponse.cs`
- `src/Hiveboard.Api/Contracts/AdminKeyInfoResponse.cs`

Update existing:
- `src/Hiveboard.Api/Program.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 5 status to Implementing.
- At the END (after acceptance criteria pass), set Task 5 status to Implemented and Task 6 status to Implementing.

Implement API key authentication and agent registration for the Hiveboard API.

Read PRD-Hiveboard.md sections 5.1 (Authentication) and 5.2 (Agent Registration & Onboarding).

1. Create AgentContext.cs — a scoped service that holds the authenticated agent's info:
   - AgentId (Guid)
   - AgentName (string)
   - AgentType (AgentType enum — Orchestrator or Worker)
   - OrganizationId (Guid)
   - IsAdmin (bool) — true if the caller used the Admin API Key

2. Create AdminKeyProvider.cs:
   - On first startup, if no admin key exists, generate one (hb_adm_... + random bytes)
   - Print it to the console: "Admin API Key: hb_adm_abc123... (save this, it won't be shown again)"
   - Store the SHA-256 hash AND the visible key prefix (first 12 characters) in the database or config file
   - Track created_at and last_used_at timestamps for the admin key
   - Also support HIVEBOARD_ADMIN_KEY environment variable (takes precedence)
   - Recovery: if the admin key is lost, setting HIVEBOARD_ADMIN_KEY and restarting overrides the stored hash

3. Create ApiKeyAuthHandler.cs — an ASP.NET Core authentication handler:
   - Reads the "X-Api-Key" header from the request
   - First checks if it matches the Admin API Key → sets IsAdmin = true
   - Otherwise hashes the key with SHA256 and looks up in the Agents table
   - If found and agent is active: sets ClaimsPrincipal with claims for AgentId, AgentType, OrganizationId
   - Also updates the agent's last_seen_at timestamp
   - If not found or agent inactive: returns 401 Unauthorized
   - Exempt the /health endpoint and /dashboard/** routes from authentication

4. Create an authorization policy "OrchestratorOnly" that requires AgentType "Orchestrator"
   Create an authorization policy "AdminOnly" that requires IsAdmin = true

5. Create AgentEndpoints.cs:
   POST /api/v1/agents/register (admin only)
   - Body: { "name": "...", "type": "worker", "platform": "claude-code", "organizationId": "..." }
   - Generate a unique API key: "hb_sk_" + 32 random bytes as hex
   - Hash with SHA256, store the hash
   - Return: { "agentId": "...", "apiKey": "hb_sk_...", "name": "...", "type": "..." }
   - The plaintext key is returned ONCE and never stored

   GET /api/v1/agents (any authenticated agent)
   - List all agents in the caller's organization
   - Include: id, name, type, platform, status, lastSeenAt

   GET /api/v1/agents/me (any authenticated agent)
   - Return the calling agent's identity and assigned tasks

   PATCH /api/v1/agents/{id} (admin only)
   - Update agent name, platform, or status (active/inactive)

   DELETE /api/v1/agents/{id} (admin only)
   - Set agent status to inactive (soft delete), revoke API key

   POST /api/v1/agents/{id}/keys/rotate (admin only)
   - Generate a new API key for the agent (hb_sk_... + 32 random bytes as hex)
   - Hash with SHA256, replace the stored hash
   - Return the new plaintext key ONCE — old key is immediately invalidated
   - The agent must be reconfigured with the new key

6. Create AdminKeyEndpoints.cs:
   GET /api/v1/admin/keys/info (admin only)
   - Return admin key metadata: prefix (first 12 chars), created_at, last_used_at
   - Never return the key itself or its hash

   POST /api/v1/admin/keys/rotate (admin only)
   - Generate a new admin key (hb_adm_... + random bytes)
   - Hash with SHA256, store the new hash and prefix, update created_at
   - Return the new plaintext admin key ONCE — old key is immediately invalidated
   - The caller must save this key immediately; subsequent requests need the new key

7. Register everything in Program.cs.

Test with: curl -H "X-Api-Key: <admin-key>" -X POST http://localhost:5000/api/v1/agents/register -d '{"name":"worker-1","type":"worker","platform":"claude-code","organizationId":"..."}'
```

**Acceptance Criteria:**
- [x] Admin API key is generated and printed on first startup
- [x] HIVEBOARD_ADMIN_KEY env var works as override
- [x] Can register a new agent with admin key → returns plaintext API key
- [x] Registered agent can authenticate with the returned key
- [x] Request without `X-Api-Key` header returns 401
- [x] Request with invalid key returns 401
- [x] GET /agents lists agents in the organization
- [x] GET /agents/me returns current agent identity
- [x] Agent's last_seen_at updates on each API call
- [x] `/health` works without authentication
- [x] `/dashboard/**` works without authentication
- [x] Admin key rotation works — old key rejected, new key works
- [x] Agent key rotation works — old agent key rejected, new key works, agent identity preserved
- [x] GET /admin/keys/info returns prefix, created_at, last_used_at (never the key itself)

---

### Task 6 — OpenAPI Documentation & Swagger Explorer
**Status:** Implemented

**Goal:** Configure OpenAPI/Swagger discoverability and document existing authentication/admin endpoints so API clients and agents can discover capabilities reliably.

**File Targets:**
Update existing:
- `src/Hiveboard.Api/Program.cs`
- `src/Hiveboard.Api/Endpoints/AgentEndpoints.cs`
- `src/Hiveboard.Api/Endpoints/AdminKeyEndpoints.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 6 status to Implementing.
- At the END (after acceptance criteria pass), set Task 6 status to Implemented and Task 7 status to Implementing.

Implement API documentation and discoverability for the Hiveboard REST API.

Read PRD-Hiveboard.md section 5.6 (API Discoverability & Documentation) and section 5.1 (Authentication).

1. Configure OpenAPI + Swagger in Program.cs:
   - Add the required service registrations for endpoint discovery and OpenAPI generation
   - Configure Swagger generation for API version "v1"
   - Define an API key security scheme for the `X-Api-Key` header
   - Add a global security requirement so protected endpoints are correctly documented
   - Expose Swagger JSON and Swagger UI in Development environment

2. Document all endpoints created so far (Task 5):
   - Add tags for endpoint groups (Agents, Admin)
   - Add summaries and descriptions for each endpoint
   - Add response metadata (at minimum: success + common error status codes)
   - Ensure auth expectations are clear in descriptions (Any authenticated, Admin only, etc.)

3. Keep behavior unchanged:
   - /health and /dashboard/** remain accessible without API key auth
   - Existing authentication and authorization behavior must continue to work

4. Use a consistent metadata style that future endpoint tasks can follow.
```

**Acceptance Criteria:**
- [ ] `dotnet build` succeeds
- [ ] Swagger JSON is available at `/swagger/v1/swagger.json` in Development
- [ ] Swagger UI is available at `/swagger` in Development
- [ ] `X-Api-Key` security scheme appears in the OpenAPI document
- [ ] All Task 5 endpoints include summary/description and response metadata
- [ ] Existing auth behavior remains unchanged (`/health` and `/dashboard/**` still public)

---

### Task 7 — Project & Epic CRUD Endpoints
**Status:** Implemented

**Goal:** Implement REST endpoints for managing projects and epics.

**File Targets:**
Create these files:
- `src/Hiveboard.Api/Endpoints/ProjectEndpoints.cs`
- `src/Hiveboard.Api/Endpoints/EpicEndpoints.cs`
- `src/Hiveboard.Api/Contracts/CreateProjectRequest.cs`
- `src/Hiveboard.Api/Contracts/CreateEpicRequest.cs`
- `src/Hiveboard.Api/Contracts/ProjectResponse.cs`
- `src/Hiveboard.Api/Contracts/EpicResponse.cs`

Update existing:
- `src/Hiveboard.Api/Program.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 7 status to Implementing.
- At the END (after acceptance criteria pass), set Task 7 status to Implemented and Task 8 status to Implementing.

Implement Project and Epic REST endpoints for the Hiveboard API.

Read PRD-Hiveboard.md sections 5.3 (Projects and Epics endpoints).

Use ASP.NET Core Minimal APIs with endpoint groups. Create static classes with extension methods to register endpoint groups.

1. ProjectEndpoints.cs — register as group "/api/v1/projects":
   - GET /api/v1/projects — list projects in the caller's organization (filter by OrganizationId from AgentContext)
   - GET /api/v1/projects/{id} — get project details (verify it belongs to caller's org)
   - POST /api/v1/projects — create project (orchestrator only). Auto-set OrganizationId from AgentContext.
   All endpoints require authorization.

2. EpicEndpoints.cs — register as group "/api/v1":
   - GET /api/v1/projects/{projectId}/epics — list epics for a project
   - POST /api/v1/projects/{projectId}/epics — create epic (orchestrator only)
   - GET /api/v1/epics/{id} — get epic with its tasks
   All endpoints require authorization.

3. Create request/response DTOs in a Contracts folder:
   - CreateProjectRequest: Name (required), Description (optional)
   - ProjectResponse: Id, Name, Description, Status, CreatedAt
   - CreateEpicRequest: Title (required), Description (optional)
   - EpicResponse: Id, ProjectId, Title, Description, Status, CreatedAt, Tasks (list, only in GET by id)

4. Validation:
   - Return 400 if required fields are missing
   - Return 404 if project/epic not found
   - Return 403 if project belongs to a different organization

5. Wire up in Program.cs using the extension methods.

Keep endpoint handlers thin and delegate to application services; application services may use DbContext directly (no repository pattern for MVP).
```

**Acceptance Criteria:**
- [x] Can create a project as orchestrator
- [x] Cannot create a project as worker (403)
- [x] Can list and get projects (both roles)
- [x] Can create epics under a project
- [x] Can get epic with its tasks
- [x] 404 for non-existent resources
- [x] Organization scoping works (can't see other orgs' projects)

---

### Task 8 — Task CRUD & Assignment Endpoints
**Status:** Implemented

**Goal:** Implement task management endpoints including creation, retrieval, assignment, and listing with filters.

**File Targets:**
Create these files:
- `src/Hiveboard.Api/Endpoints/TaskEndpoints.cs`
- `src/Hiveboard.Api/Contracts/CreateTaskRequest.cs`
- `src/Hiveboard.Api/Contracts/UpdateTaskRequest.cs`
- `src/Hiveboard.Api/Contracts/TaskResponse.cs`
- `src/Hiveboard.Api/Contracts/TaskDetailResponse.cs`

Update existing:
- `src/Hiveboard.Api/Program.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 8 status to Implementing.
- At the END (after acceptance criteria pass), set Task 8 status to Implemented and Task 9 status to Implementing.

Implement Task CRUD and assignment endpoints for the Hiveboard API.

Read PRD-Hiveboard.md sections 4.2 (Task entity), 5.3 (Task endpoints), and 5.5 (Full Task Context Response).

1. TaskEndpoints.cs — register endpoints:

   GET /api/v1/projects/{projectId}/tasks
   - List tasks with optional query filters: ?status=InProgress&agentId=xxx&epicId=xxx
   - Returns TaskResponse[] (lightweight, no nested context)

   POST /api/v1/projects/{projectId}/tasks (orchestrator only)
   - Creates a task in "backlog" status
   - Fields: Title (required), Description, EpicId (optional), ParentTaskId (optional), Metadata (optional)

   GET /api/v1/tasks/{id}
   - Returns FULL task context (TaskDetailResponse):
     - Task fields
     - Epic info (if linked)
     - Parent task info (if subtask)
     - Subtasks list
     - Dependencies (blocked_by and blocking, each with task id, title, status)
     - Notes (with agent name, type, content, timestamp)
     - Events (audit trail)
     - Related decision records
   - This is the primary endpoint agents use to understand their work

   PATCH /api/v1/tasks/{id} (orchestrator only)
   - Update task fields: Title, Description, EpicId, AssignedAgentId, Metadata
   - For assignment: if AssignedAgentId is provided and task is in "backlog",
     transition to "assigned" automatically
   - Return 409 if task is already assigned to a different agent

2. Request/Response DTOs:
   - CreateTaskRequest: Title, Description, EpicId?, ParentTaskId?, Metadata?
   - UpdateTaskRequest: Title?, Description?, EpicId?, AssignedAgentId?, Metadata?
   - TaskResponse: Id, Title, Status, AssignedAgentId, EpicId, CreatedAt, UpdatedAt (list view)
   - TaskDetailResponse: Full nested response matching PRD section 5.4

3. When a task is assigned, create a TaskEvent (event_type: "assigned") and a
   Notification for the assigned worker agent (type: TaskAssigned).

4. Validate that the assigned agent exists, belongs to the same org, and is a worker.
```

**Acceptance Criteria:**
- [x] Can create tasks under a project (orchestrator)
- [x] Can list tasks with status/agent/epic filters
- [x] GET /tasks/{id} returns full nested context (dependencies, notes, events, decisions)
- [x] Assignment works — sets agent and transitions to "assigned"
- [x] 409 if task already assigned to different agent
- [x] TaskEvent is recorded on assignment
- [x] Notification is created for assigned agent

---

### Task 9 — Coordinator-First MVP Realignment
**Status:** Implemented

**Goal:** Retrofit the code from Tasks 1–8 to the new coordinator-first MVP without removing optional co-orchestrator support.

**File Targets:**
Create these files:
- `src/Hiveboard.Api/Application/CoordinatorScopeResolver.cs`
- `tests/Hiveboard.Tests/CoordinatorControlIntegrationTests.cs`

Generated by tooling (only if a schema change is required):
- `src/Hiveboard.Infrastructure/Data/Migrations/` (EF Core migration for coordinator-first alignment)

Update existing:
- `src/Hiveboard.Api/Auth/AgentContext.cs`
- `src/Hiveboard.Api/Auth/ApiKeyAuthHandler.cs`
- `src/Hiveboard.Api/Application/AgentAccessGuard.cs`
- `src/Hiveboard.Api/Application/AgentApplicationService.cs`
- `src/Hiveboard.Api/Application/AgentContextFactory.cs`
- `src/Hiveboard.Api/Application/HiveboardApplicationServiceCollectionExtensions.cs`
- `src/Hiveboard.Api/Application/ProjectApplicationService.cs`
- `src/Hiveboard.Api/Application/EpicApplicationService.cs`
- `src/Hiveboard.Api/Application/TaskApplicationService.cs`
- `src/Hiveboard.Api/Endpoints/ProjectEndpoints.cs`
- `src/Hiveboard.Api/Endpoints/EpicEndpoints.cs`
- `src/Hiveboard.Api/Endpoints/TaskEndpoints.cs`
- `src/Hiveboard.Api/Program.cs`
- `src/Hiveboard.Api/Hiveboard.Api.csproj`
- `src/Hiveboard.Infrastructure/Data/HiveboardDbSeeder.cs`
- `src/Hiveboard.Dashboard/package.json`
- `src/Hiveboard.Dashboard/vite.config.ts`
- `src/Hiveboard.Dashboard/src/App.tsx`
- `tests/Hiveboard.Tests/Infrastructure/HiveboardApiFactory.cs`
- `tests/Hiveboard.Tests/Infrastructure/IntegrationTestData.cs`
- `tests/Hiveboard.Tests/ProjectEpicCrudIntegrationTests.cs`
- `tests/Hiveboard.Tests/TaskCrudAssignmentIntegrationTests.cs`
- `tests/Hiveboard.Tests/TenantScopingIntegrationTests.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 9 status to Implementing.
- At the END (after acceptance criteria pass), set Task 9 status to Implemented and Task 10 status to Implementing.

Retrofit the current codebase from Tasks 1–8 so Hiveboard matches the updated coordinator-first MVP.

Read README.md, PRD-Hiveboard.md sections 1, 3.3, 5.1, 5.2, 8, and 9, and inspect the current code produced by Tasks 1–8.

Important framing:
- Human coordinators are now the default controlling actor in the MVP.
- The Admin API key is also the self-hosted coordinator credential for control-plane actions.
- Orchestrator agents remain optional and should continue to work, but they are no longer required for normal project/task CRUD flows.

1. Coordinator scope + authorization:
   - Add a CoordinatorScopeResolver service that resolves the effective organization for the admin/coordinator credential in the self-hosted MVP.
   - For the current MVP, admin/coordinator requests should work against the single/default organization. Return a clear error if no organization can be resolved.
   - Update AgentContext / auth plumbing so coordinator/admin calls have an effective organization scope when needed.
   - Replace orchestrator-only checks on project, epic, task create/update/assignment flows with coordinator-or-orchestrator checks.
   - Keep worker restrictions unchanged.
   - Update endpoint summaries/descriptions and human-facing error messages to use "coordinator" for the controlling actor.
   - When a project is created by a coordinator/admin, leave the project usable with no orchestrator attached.
   - When a project is created by an orchestrator agent, preserve the optional project-orchestrator link.

2. Database, migration, and seeding:
   - Review the existing EF model and schema for any orchestrator-first coupling that blocks the new behavior.
   - If schema changes are required to support coordinator scope resolution or optional project orchestration, add an EF Core migration and keep SQLite/PostgreSQL compatibility.
   - Update development seed data so a fresh dev database reflects the new MVP:
     - one default organization
     - one sample project that is coordinator-managed by default
     - worker agents with known dev keys
     - optional co-orchestrator sample data only if it is not required for the main happy path
   - Ensure startup migration + seeding still work idempotently on a fresh database.

3. Tests:
   - Update the Task 7/8 integration tests so human-driven flows use the coordinator/admin credential instead of requiring an orchestrator agent.
   - Add a dedicated regression test for coordinator-controlled CRUD/assignment behavior.
   - Cover:
     - coordinator/admin can list/create/update projects, epics, and tasks in the resolved organization
     - workers remain forbidden from coordinator-only actions
     - a coordinator-created project has no orchestrator attached by default
     - an orchestrator-created project still attaches that orchestrator when one is used
   - Update test fixtures/helpers to support coordinator scope resolution.

4. Dashboard alignment:
   - Stop treating the dashboard as a build-time dependency of the API by default.
   - Make normal backend builds skip dashboard asset generation unless explicitly opted in.
   - Keep the dashboard as a standalone Vite app in the monorepo.
   - Replace the Vite starter UI with a minimal Hiveboard control-plane placeholder so the app no longer looks like template scaffolding.
   - Ensure the dashboard still builds independently with npm.

5. Validation:
   - Use the sandbox-safe validation sequence from AGENTS.md:
     - dotnet restore Hiveboard.sln
     - dotnet build Hiveboard.sln -p:BuildDashboardAssets=false
     - dotnet test tests/Hiveboard.Tests/Hiveboard.Tests.csproj --no-restore -p:UseSandboxBuildWorkaround=true -p:BuildDashboardAssets=false
   - Also run npm run build in src/Hiveboard.Dashboard.
```

**Acceptance Criteria:**
- [x] Coordinator/admin can create projects, epics, and tasks and assign workers in the single-organization MVP flow
- [x] Workers still receive 403 for coordinator-only create/update/assignment actions
- [x] A coordinator-created project has no orchestrator attached by default
- [x] An orchestrator-created project still links that orchestrator to the project
- [x] Development seeding produces a coordinator-managed sample organization/project
- [x] Any required EF Core migration compiles and applies cleanly on a fresh database
- [x] Existing Task 7/8 integration tests are updated and a coordinator regression test is added
- [x] `dotnet restore Hiveboard.sln` succeeds
- [x] `dotnet build Hiveboard.sln -p:BuildDashboardAssets=false` succeeds
- [x] `dotnet test tests/Hiveboard.Tests/Hiveboard.Tests.csproj --no-restore -p:UseSandboxBuildWorkaround=true -p:BuildDashboardAssets=false` succeeds
- [x] `npm run build` succeeds in `src/Hiveboard.Dashboard`
- [x] Backend builds no longer require dashboard asset generation by default

---

### Task 10 — Task State Machine
**Status:** Implemented

**Goal:** Implement the task status transition endpoint with coordinator-first rules while preserving optional co-orchestrator compatibility.

**File Targets:**
Create these files:
- `src/Hiveboard.Core/Services/TaskStateMachine.cs`
- `src/Hiveboard.Api/Endpoints/TaskStatusEndpoints.cs`
- `src/Hiveboard.Api/Contracts/UpdateTaskStatusRequest.cs`

Update existing:
- `src/Hiveboard.Api/Program.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 10 status to Implementing.
- At the END (after acceptance criteria pass), set Task 10 status to Implemented and Task 11 status to Implementing.

Implement the task state machine for the Hiveboard API.

Read PRD-Hiveboard.md section 6 (Task Workflow) very carefully — the state transition table and rules.

Use the coordinator-first auth model introduced in Task 9. The human coordinator is the
default controlling actor, but an optional orchestrator agent should still be able to
perform coordinator-side actions when one is configured.

1. Create TaskStateMachine.cs in Hiveboard.Core/Services/:
   - A service class (register as scoped) that encapsulates all transition logic
   - Method: ValidateTransition(AgentTask task, TaskStatus newStatus, AgentContext caller)
     Returns a result with success/failure and error message
   - Transition rules from PRD section 6.2:
     - backlog → assigned: coordinator or optional orchestrator, must provide agent assignment
     - assigned → in-progress: assigned agent only, ALL blocking dependencies must be "done"
     - in-progress → in-review: assigned agent only
     - in-progress → blocked: assigned agent only, requires blocked_reason
     - in-progress → done: assigned agent only
     - in-review → in-progress: coordinator or optional orchestrator (send back)
     - in-review → done: coordinator or optional orchestrator (approve)
     - blocked → assigned: coordinator or optional orchestrator, clears blocked_reason
     - Any → backlog: coordinator or optional orchestrator, unassigns agent
   - Any transition not in this list returns an error

2. Create the endpoint:
   PATCH /api/v1/tasks/{id}/status
   - Body: { "status": "InProgress", "blockedReason": "..." }
   - Uses TaskStateMachine to validate
   - On success: updates the task, creates a TaskEvent
   - On validation failure: returns 409 with the error message
   - On dependency violation: returns 409 with list of unmet dependencies

3. Side effects on transition:
   - → in-review: Create Notification for coordinator (type: ReviewRequested);
     if the project has an optional orchestrator agent configured, notify that agent too
   - → blocked: Create Notification for coordinator (type: TaskBlocked, message includes blocked_reason);
     if the project has an optional orchestrator agent configured, notify that agent too
   - → done: Find all tasks that depend on this one, check if they're now unblocked,
     create Notification for their assigned agents (type: DependencyResolved)
   - → done (parent auto-complete): If this task has a parent and ALL sibling subtasks are "done",
     auto-transition the parent to "done"

4. Worker agents can only transition tasks assigned to them.
   Coordinator can transition any task in their org's projects.
   Optional orchestrator agents can perform the same coordination-side transitions when enabled.
```

**Acceptance Criteria:**

- [x] All valid transitions from PRD section 6.2 work correctly
- [x] Invalid transitions return 409 with clear error message
- [x] Coordinator can perform coordination-side transitions
- [x] Optional orchestrator can perform the same coordination-side transitions when configured
- [x] Worker can't transition tasks not assigned to them
- [x] Transitioning to in-progress with unmet dependencies returns 409
- [x] Moving a task to in-review notifies the coordinator
- [x] Blocking a task notifies the coordinator
- [x] Coordinator-facing state-change notifications also reach the optional orchestrator when configured
- [x] Completing a task notifies dependent task agents
- [x] Parent auto-completes when all subtasks are done
- [x] TaskEvent is recorded for every transition

---

### Task 11 — Dependency Management
**Status:** Implemented

**Goal:** Implement task dependency CRUD with circular dependency detection for coordinator-first workflows while preserving optional co-orchestrator compatibility.

**File Targets:**
Create these files:
- `src/Hiveboard.Core/Services/DependencyService.cs`
- `src/Hiveboard.Api/Endpoints/DependencyEndpoints.cs`
- `src/Hiveboard.Api/Contracts/CreateDependencyRequest.cs`
- `src/Hiveboard.Api/Contracts/DependencyGraphResponse.cs`

Update existing:
- `src/Hiveboard.Api/Program.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 11 status to Implementing.
- At the END (after acceptance criteria pass), set Task 11 status to Implemented and Task 12 status to Implementing.

Implement task dependency management for the Hiveboard API.

Read PRD-Hiveboard.md sections 4.2 (TaskDependency), 5.3 (Dependency endpoints), and 5.5 (Dependency Enforcement).

1. Create DependencyService.cs in Hiveboard.Core/Services/:
   - Method: AddDependency(Guid taskId, Guid dependsOnTaskId) → Result
     - Validates both tasks exist and belong to the same project
     - Prevents self-dependency
     - Checks for circular dependencies using DFS/BFS graph traversal:
       Starting from dependsOnTaskId, follow its dependencies recursively.
       If we reach taskId, it's circular → return error with the cycle path.
     - Creates the TaskDependency record
   - Method: GetDependencyGraph(Guid projectId) → graph structure
     - Returns all tasks and their dependency edges for the project
     - Include task id, title, status for each node

2. Create endpoints:
   POST /api/v1/tasks/{id}/dependencies (coordinator or optional orchestrator)
   - Body: { "dependsOnTaskId": "..." }
   - Returns 400 if circular dependency detected (include the cycle in error message)
   - Returns 400 if self-dependency
   - Returns 404 if either task doesn't exist

   DELETE /api/v1/tasks/{id}/dependencies/{depId} (coordinator or optional orchestrator)
   - Removes a dependency

   GET /api/v1/projects/{projectId}/dependencies/graph (any authenticated agent)
   - Returns the full dependency graph for visualization

3. Graph response format:
   {
     "nodes": [
       { "taskId": "...", "title": "...", "status": "InProgress", "assignedAgentId": "..." }
     ],
     "edges": [
       { "from": "task-1-id", "to": "task-2-id", "type": "blocks" }
     ]
   }
```

**Acceptance Criteria:**
- [x] Can add dependencies between tasks
- [x] Circular dependency detection works (A→B→C→A returns error with cycle path)
- [X] Self-dependency returns 400
- [X] Cross-project dependencies are rejected
- [x] Can remove dependencies
- [X] Dependency graph endpoint returns correct nodes and edges
- [X] Coordinator can add/remove dependencies
- [x] Optional orchestrator can also add/remove dependencies when configured

---

### Task 12 — Task Decomposition
**Status:** Implemented

**Goal:** Implement the task decomposition endpoint where a worker, coordinator, or optional orchestrator breaks a task into subtasks.

**File Targets:**
Create these files:
- `src/Hiveboard.Api/Endpoints/DecompositionEndpoints.cs`
- `src/Hiveboard.Api/Contracts/DecomposeTaskRequest.cs`

Update existing:
- `src/Hiveboard.Api/Program.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 12 status to Implementing.
- At the END (after acceptance criteria pass), set Task 12 status to Implemented and Task 13 status to Implementing.

Implement task decomposition for the Hiveboard API.

Read PRD-Hiveboard.md sections 5.3 (POST /tasks/{id}/subtasks) and 6.3 (Task Decomposition).

1. Create the endpoint:
   POST /api/v1/tasks/{id}/subtasks
   - Auth: the agent assigned to this task (worker) OR coordinator OR optional orchestrator
   - Body: { "subtasks": [{ "title": "...", "description": "..." }, ...] }
   - Validates the parent task exists and belongs to the caller's org
   - Validates the parent task is in "assigned" or "in-progress" status

2. On decomposition:
   - Create each subtask as a new AgentTask:
     - ParentTaskId = the parent task's id
     - ProjectId = inherited from parent
     - EpicId = inherited from parent
     - Status = Backlog
   - Move the parent task to "in-progress" if it's in "assigned"
   - Create a TaskEvent on the parent (event_type: "decomposed", new_value: count of subtasks)
   - Create a Notification for the coordinator (type: TaskDecomposed,
     message: "Task '{title}' was decomposed into {n} subtasks by {agent_name}")
   - If the project has an optional orchestrator agent configured, notify that agent too

3. The parent task should NOT be completable directly once it has subtasks.
   It auto-completes when all subtasks reach "done" (this logic is in Task 10's state machine).

4. Return the created subtasks with their IDs and status.

5. Validation:
   - At least 1 subtask required
   - Each subtask must have a non-empty title
   - Max 50 subtasks per decomposition request
```

**Acceptance Criteria:**
- [x] Assigned worker can decompose their task into subtasks
- [x] Coordinator can also decompose tasks
- [x] Optional orchestrator can also decompose tasks when configured
- [x] Subtasks are created in backlog with correct parent/project/epic links
- [x] Parent moves to in-progress if it was assigned
- [x] Coordinator is notified of decomposition
- [x] Coordinator-facing decomposition notifications also reach the optional orchestrator when configured
- [x] TaskEvent is recorded
- [x] Validation: empty title, 0 subtasks, >50 subtasks all return 400

---

### Task 13 — Notes & Decision Records Endpoints
**Status:** Implemented

**Goal:** Implement task notes and project-level decision record endpoints.

**File Targets:**
Create these files:
- `src/Hiveboard.Api/Endpoints/NoteEndpoints.cs`
- `src/Hiveboard.Api/Endpoints/DecisionEndpoints.cs`
- `src/Hiveboard.Api/Contracts/CreateNoteRequest.cs`
- `src/Hiveboard.Api/Contracts/NoteResponse.cs`
- `src/Hiveboard.Api/Contracts/CreateDecisionRequest.cs`
- `src/Hiveboard.Api/Contracts/DecisionResponse.cs`

Update existing:
- `src/Hiveboard.Api/Program.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 13 status to Implementing.
- At the END (after acceptance criteria pass), set Task 13 status to Implemented and Task 14 status to Implementing.

Implement Notes and Decision Records endpoints for the Hiveboard API.

Read PRD-Hiveboard.md sections 4.2 (TaskNote, DecisionRecord) and 5.3 (Notes, Decision endpoints).

1. NoteEndpoints.cs:
   POST /api/v1/tasks/{id}/notes (any authenticated agent)
   - Body: { "content": "markdown text...", "noteType": "Context" }
   - Auto-sets AgentId from the caller
   - Creates a TaskEvent (event_type: "note_added")
   - noteType values: Context, Progress, ReviewRequest, Blocker, Resolution

   GET /api/v1/tasks/{id}/notes (any authenticated agent)
   - Returns all notes for a task, ordered by CreatedAt ascending
   - Include agent name and type in each note response

2. DecisionEndpoints.cs:
   POST /api/v1/projects/{projectId}/decisions (any authenticated agent)
   - Body: { "title": "...", "content": "free-form markdown", "taskId": null, "status": "Proposed" }
   - content is free-form markdown for rationale, alternatives, rejected approaches
   - taskId is optional — links the decision to a specific task

   GET /api/v1/projects/{projectId}/decisions (any authenticated agent)
   - List all decisions for a project, ordered by CreatedAt descending
   - Filterable by ?status=Accepted&taskId=xxx

   GET /api/v1/decisions/{id} (any authenticated agent)
   - Get a single decision record

3. Both notes and decisions are readable by any agent in the organization.
   This is how agents share context and decisions with each other.
```

**Acceptance Criteria:**
- [x] Can add notes to tasks (both coordinator and worker)
- [x] Notes include agent name in response
- [x] Can create decision records linked to tasks or standalone
- [x] Decision records support free-form markdown content
- [x] Can filter decisions by status and task
- [x] TaskEvent is created when a note is added

---

## Phase 3: Coordination + Intelligence

### Task 14 — Notification Engine
**Status:** Implemented

**Goal:** Implement the notification polling endpoint and ensure all coordinator/worker notification triggers from previous tasks are working correctly.

**File Targets:**
Create these files:
- `src/Hiveboard.Api/Endpoints/NotificationEndpoints.cs`
- `src/Hiveboard.Api/Contracts/NotificationResponse.cs`
- `src/Hiveboard.Core/Services/NotificationService.cs`

Update existing (refactor notification creation to use the new service):
- `src/Hiveboard.Api/Endpoints/TaskEndpoints.cs`
- `src/Hiveboard.Api/Endpoints/TaskStatusEndpoints.cs`
- `src/Hiveboard.Api/Endpoints/DecompositionEndpoints.cs`
- `src/Hiveboard.Api/Program.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 14 status to Implementing.
- At the END (after acceptance criteria pass), set Task 14 status to Implemented and Task 15 status to Implementing.

Implement the notification system for the Hiveboard API.

Read PRD-Hiveboard.md section 7 (Notification System).

1. Create NotificationService.cs in Hiveboard.Core/Services/:
   - Method: CreateNotification(Guid agentId, NotificationType type, Guid taskId, string message)
   - Method: GetUnacknowledged(Guid agentId) → List<Notification>
   - Method: Acknowledge(Guid notificationId, Guid agentId) → bool
   - Register as scoped service

2. Refactor all existing notification creation in Tasks 8, 10, 12 to use this service
   instead of creating Notification entities directly. Search the codebase for any place
   a Notification is created and route it through NotificationService.

3. Create NotificationEndpoints.cs:
   GET /api/v1/agents/me/notifications
   - Returns unacknowledged notifications for the calling agent
   - Ordered by CreatedAt descending
   - Include task title in the response (join with AgentTask)

   POST /api/v1/agents/me/notifications/{id}/ack
   - Marks a notification as acknowledged
   - Returns 404 if notification doesn't belong to the caller

4. Notification scenarios (verify all are working):
   - TaskAssigned: when coordinator assigns a task to a worker
   - TaskBlocked: when worker marks task as blocked → notify coordinator
   - TaskDecomposed: when worker decomposes task → notify coordinator
   - DependencyResolved: when a blocking task completes → notify agents on dependent tasks
   - ReviewRequested: when worker moves task to in-review → notify coordinator
   - For coordinator-facing notifications, also notify the optional project orchestrator when one is configured

5. Response format:
   {
     "id": "...",
     "type": "TaskBlocked",
     "taskId": "...",
     "taskTitle": "...",
     "message": "Agent 'Worker-1' blocked task 'Setup Auth': waiting for API design decision",
     "createdAt": "...",
     "isAcknowledged": false
   }
```

**Acceptance Criteria:**
- [x] Can poll for unacknowledged notifications
- [x] Can acknowledge notifications
- [x] All 5 notification types fire correctly
- [x] Notifications are agent-scoped (can only see your own)
- [x] Acknowledging another agent's notification returns 404

---

### Task 15 — Full Task Context Assembly
**Status:** Implemented

**Goal:** Ensure the GET /tasks/{id} endpoint returns the complete, rich context bundle that agents and human coordinators need.

**File Targets:**
Create these files:
- `src/Hiveboard.Core/Services/TaskContextService.cs`

Update existing:
- `src/Hiveboard.Api/Endpoints/TaskEndpoints.cs` (use TaskContextService)
- `src/Hiveboard.Api/Contracts/TaskDetailResponse.cs` (enrich response shape)
- `src/Hiveboard.Api/Endpoints/AgentEndpoints.cs` (enrich /agents/me response)

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 15 status to Implementing.
- At the END (after acceptance criteria pass), set Task 15 status to Implemented and Task 16 status to Implementing.

Implement the full task context assembly for GET /api/v1/tasks/{id}.

Read PRD-Hiveboard.md section 5.5 (Key API Behaviors — Full Task Context Response).

This is the most important endpoint in the system — it's what agents call to understand
their assigned work, and what the human control plane uses to inspect and intervene.
The response must include EVERYTHING a coordinator or worker needs.

1. Create TaskContextService.cs in Hiveboard.Core/Services/:
   - Method: GetFullContext(Guid taskId) → TaskDetailResponse
   - Assembles the complete context in a single service call using efficient EF Core queries
   - Use Include/ThenInclude or split queries to avoid N+1

2. The response must include:
   - task: all task fields including metadata
   - epic: { id, title, description, status } if task is linked to an epic, else null
   - parentTask: { id, title, status } if this is a subtask, else null
   - subtasks: [{ id, title, status, assignedAgentName }] — direct children only
   - dependencies:
     - blockedBy: tasks that must complete before this one [{ taskId, title, status }]
     - blocking: tasks waiting for this one [{ taskId, title, status }]
   - notes: all notes ordered chronologically, each with { agentName, agentType, noteType, content, createdAt }
   - events: audit trail [{ eventType, agentName, oldValue, newValue, timestamp }]
   - relatedDecisions: decision records linked to this task [{ id, title, status, content, agentName, createdAt }]
   - project: { id, name } for context

3. Update the GET /tasks/{id} endpoint to use TaskContextService.

4. Also update GET /api/v1/agents/me to return:
   - Agent identity (id, name, type, platform)
   - Currently assigned tasks (list with id, title, status)
   - Unacknowledged notification count
```

**Acceptance Criteria:**
- [x] GET /tasks/{id} returns complete nested response matching PRD section 5.4
- [x] All relationships are populated (epic, parent, subtasks, dependencies, notes, events, decisions)
- [x] No N+1 query issues (check SQL output with logging)
- [x] GET /agents/me includes assigned tasks and notification count
- [x] Null fields are handled gracefully (no errors when epic/parent is null)

---

## Phase 4: MCP + Control Plane

### Task 16 — MCP Server Interface
**Status:** Implemented

**Goal:** Expose Hiveboard functionality via MCP (Model Context Protocol) so worker agents and optional co-orchestrator agents can integrate with zero custom code.

**File Targets:**
Create these files:
- `src/Hiveboard.Api/Mcp/HiveboardMcpServer.cs`
- `src/Hiveboard.Api/Mcp/McpTools.cs`
- `src/Hiveboard.Api/Mcp/McpResources.cs`
- `mcp-config.json` (example config for agent setup)

Update existing:
- `src/Hiveboard.Api/Program.cs`
- `src/Hiveboard.Api/Hiveboard.Api.csproj` (add MCP SDK NuGet package)

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 16 status to Implementing.
- At the END (after acceptance criteria pass), set Task 16 status to Implemented and Task 17 status to Implementing.

Implement an MCP (Model Context Protocol) server interface for Hiveboard.

Read PRD-Hiveboard.md sections 5.4 (MCP Server Interface) and 5.7 (MCP Discoverability & Contract Stability).

Use the official .NET MCP SDK NuGet package (ModelContextProtocol or the official Microsoft/Anthropic package — 
check NuGet for the latest). If no stable .NET MCP SDK exists, implement a minimal MCP server 
using the MCP specification over stdio or SSE transport.

1. MCP Tools to expose (each wraps the corresponding REST logic):
   - hiveboard_list_tasks: List tasks with optional filters (projectId, status, agentId)
   - hiveboard_get_task: Get full task context by ID
   - hiveboard_update_status: Transition a task's status
   - hiveboard_add_note: Add a note to a task
   - hiveboard_decompose_task: Break a task into subtasks
   - hiveboard_add_decision: Record an architectural decision
   - hiveboard_get_dependencies: Get dependency graph for a project
   - hiveboard_my_tasks: Get the calling agent's assigned tasks and notifications
   - hiveboard_get_notifications: Get unacknowledged notifications

2. MCP Resources to expose:
   - hiveboard://project/{id}/overview — project summary with stats
   - hiveboard://task/{id}/context — full task context bundle
   - hiveboard://project/{id}/decisions — all decision records

3. Authentication for MCP:
   - MCP connections should be authenticated via the same API key mechanism
   - The API key can be provided as a configuration parameter when the MCP server starts
   - Or via an environment variable HIVEBOARD_API_KEY

4. Register the MCP server alongside the REST API in Program.cs.
   The MCP server should use SSE transport on a separate endpoint path (e.g., /mcp)
   OR stdio transport if running as a standalone process.

5. Create an mcp-config.json example showing how to configure Hiveboard as an MCP server
   in Claude Code, Copilot, and Cursor settings.

6. MCP contract quality requirements:
   - Ensure `list_tools` exposes all tools with stable names, clear descriptions, and input schemas
   - Ensure `list_resources` exposes all resources with URI patterns and descriptions
   - Keep tool/resource names exactly as defined in the PRD (no renames in patch changes)
   - Return structured MCP errors (machine-readable code + message) for validation failures,
     unauthorized access, not found entities, and conflict/business-rule violations

7. Add a small local verification note in comments/README for how to smoke-test:
   - Connect with an MCP inspector/client
   - Run `list_tools` and `list_resources`
   - Invoke at least one successful tool call and one invalid-input call
```

**Acceptance Criteria:**
- [x] MCP server starts alongside the REST API
- [x] All 9 tools are listed when an MCP client connects
- [x] All 3 resources are accessible
- [x] Tool calls work correctly and return proper results
- [x] `list_tools` metadata includes descriptions and input schema for each tool
- [x] `list_resources` metadata includes descriptions for each resource
- [x] Invalid tool input returns structured MCP validation errors
- [x] Unauthorized MCP requests return structured auth errors
- [x] Example MCP config file is provided for agent setup

---

### Task 17 — Dashboard Control Plane (React SPA)
**Status:** Implemented

**Goal:** Implement a React dashboard as the human control plane for Hiveboard, plus an Admin Panel for key management.

**File Targets:**
Create these files (the React project should already be scaffolded from Task 1):
- `src/Hiveboard.Dashboard/src/App.tsx`
- `src/Hiveboard.Dashboard/src/pages/ProjectOverview.tsx`
- `src/Hiveboard.Dashboard/src/pages/TaskBoard.tsx`
- `src/Hiveboard.Dashboard/src/pages/AgentActivity.tsx`
- `src/Hiveboard.Dashboard/src/pages/EventTimeline.tsx`
- `src/Hiveboard.Dashboard/src/pages/DecisionLog.tsx`
- `src/Hiveboard.Dashboard/src/pages/CoordinatorConsole.tsx`
- `src/Hiveboard.Dashboard/src/pages/AdminPanel.tsx`
- `src/Hiveboard.Dashboard/src/api/client.ts`
- `src/Hiveboard.Dashboard/src/components/` (shared components)

Update existing:
- `src/Hiveboard.Api/Program.cs` (optional bundled static serving)
- `src/Hiveboard.Dashboard/package.json`
- `src/Hiveboard.Dashboard/vite.config.ts`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 17 status to Implementing.
- At the END (after acceptance criteria pass), set Task 17 status to Implemented and Task 18 status to Implementing.

Implement the React dashboard for Hiveboard.

Read PRD-Hiveboard.md section 8 (Dashboard).

The dashboard is the human control plane for Hiveboard. It must support both visibility
and intervention: task creation, assignment, review approval/send-back, and blocker
resolution. The Admin Panel still handles key management operations.

Technical stack: React + TypeScript + Vite + Tailwind CSS. The project is at src/Hiveboard.Dashboard/.
Treat it as a standalone frontend app in the monorepo. It should run independently in
development and may optionally be bundled into the API host for self-hosted deployment.

1. API Client (src/api/client.ts):
   - Create a typed API client that calls the Hiveboard REST API at /api/v1/...
   - Support configurable base URL via `VITE_API_BASE_URL`
   - Add a usePolling hook that re-fetches data every 10 seconds

2. Authentication model:
   - On first visit, prompt for the Admin API Key (or coordinator credential for the
     current MVP auth model)
   - Store it in **session storage only** (never localStorage or cookies)
   - Send it as the `X-Api-Key` header for privileged requests

3. React Router with these routes:

   a) Project Overview (route: `/`)
   - List of projects with: name, task count by status, active agent count, completion %
   - Click a project to go to its task board

   b) Task Board (route: `/projects/:id/board`)
   - Kanban columns: Backlog | Assigned | In Progress | In Review | Done | Blocked
   - Each card shows: task title, assigned agent name, time in current status
   - Cards in "blocked" column highlighted in red/orange
   - Support create/edit/assign/reassign flows for coordinators

   c) Agent Activity (route: `/agents`)
   - List of agents: name, platform, type, current task, last activity timestamp
   - Online/offline indicator based on last_seen_at (>5 min = offline)

   d) Event Timeline (route: `/projects/:id/timeline`)
   - Chronological feed of task events across the project
   - Show: timestamp, agent name, event type, task title, old→new value

   e) Decision Log (route: `/projects/:id/decisions`)
   - List of decision records: title, status, agent, date
   - Click to expand and see full markdown content (use react-markdown)

   f) Coordinator Console (route: `/projects/:id/console`)
   - Show tasks awaiting review, blocked tasks, and assignment gaps
   - Allow approve/send-back from in-review
   - Allow blocker resolution and reassignment

   g) Admin Panel (route: `/admin`)
   - Admin Key section:
     - Show key metadata: prefix (first 12 chars), created_at, last_used_at
     - "Rotate Admin Key" button: calls POST /admin/keys/rotate, displays the new key
       ONCE in a copy-to-clipboard modal, warns the user to save it
   - Agent Keys section:
     - List all agents with: name, platform, type, status, key prefix
     - "Rotate Key" button per agent: calls POST /agents/{id}/keys/rotate,
       displays the new agent key ONCE in a copy-to-clipboard modal
   - If admin key is invalid or expired, show an auth error and prompt for re-entry

4. Styling: Tailwind CSS with dark theme. Clean, functional, not fancy.

5. Build and serve:
   - `npm run dev` should run the dashboard as a standalone app
   - `npm run build` should produce a deployable `dist/`
   - Add an optional bundled build path for self-hosted packaging:
     - either `npm run build:bundle`
     - or an API publish/build step that copies `dist/` into `src/Hiveboard.Api/wwwroot/dashboard/`
   - Update Hiveboard.Api Program.cs to serve bundled assets from `/dashboard` when present
   - Do NOT require the dashboard build to run on every `dotnet build`

6. Install these npm packages: react-router-dom, react-markdown, tailwindcss

7. The dependency graph view can be deferred — just show a list of dependencies for now.
```

**Acceptance Criteria:**
- [ ] `npm run build` in Hiveboard.Dashboard succeeds
- [ ] Dashboard works in standalone dev mode
- [ ] Optional bundled mode works at `/dashboard` when assets are present
- [ ] Project overview shows all projects with stats
- [ ] Task board displays kanban columns with correct cards
- [ ] Coordinators can create, edit, assign, and reassign tasks
- [ ] Agent activity shows agent list with status
- [ ] Event timeline shows chronological events
- [ ] Decision log renders markdown content
- [ ] Coordinator Console supports approve/send-back and blocker resolution
- [ ] Auto-refresh works (new data appears within 10 seconds)
- [ ] Client-side routing works in standalone mode and bundled mode
- [ ] Admin Panel prompts for admin/coordinator key and stores it in session storage
- [ ] Admin Panel shows admin key metadata (prefix, timestamps)
- [ ] Admin key rotation works from the dashboard — new key shown once
- [ ] Agent key rotation works from the dashboard — new key shown once
- [ ] Invalid/expired admin key shows auth error and re-prompts

---

## Phase 5: Polish

### Task 18 — Logging, Health Checks, Error Handling
**Status:** Implementing

**Goal:** Add structured logging, comprehensive health checks, and consistent error responses.

**File Targets:**
Create these files:
- `src/Hiveboard.Api/Middleware/ErrorHandlingMiddleware.cs`
- `src/Hiveboard.Api/Contracts/ErrorResponse.cs`

Update existing:
- `src/Hiveboard.Api/Program.cs`
- `src/Hiveboard.Api/Hiveboard.Api.csproj` (add Serilog, rate limiting NuGet packages)

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 18 status to Implementing.
- At the END (after acceptance criteria pass), set Task 18 status to Implemented and Task 19 status to Implementing.

Add production-readiness features to the Hiveboard API.

Read PRD-Hiveboard.md section 10 (Non-Functional Requirements).

1. Structured Logging:
   - Add Serilog NuGet packages: Serilog.AspNetCore, Serilog.Sinks.Console, Serilog.Sinks.File
   - Configure in Program.cs: console sink (structured JSON) + file sink (logs/hiveboard-.log, rolling daily)
   - Log: API requests (method, path, status, duration), auth failures, task transitions, errors
   - Do NOT log API keys or agent secrets

2. Error Handling Middleware:
   - Global exception handler that catches unhandled exceptions
   - Returns consistent ErrorResponse: { "error": "...", "detail": "...", "traceId": "..." }
   - In development: include exception details
   - In production: generic message only, log the full exception
   - Handle specific exceptions:
     - KeyNotFoundException → 404
     - UnauthorizedAccessException → 403
     - InvalidOperationException → 409
     - ArgumentException → 400
     - Everything else → 500

3. Health Checks:
   - Update /health to include:
     - Database connectivity check (can connect and query)
     - System info (uptime, version)
   - Use ASP.NET Core health checks framework
   - Response: { "status": "healthy", "checks": { "database": "healthy" }, "uptime": "...", "version": "1.0.0" }

4. Request Validation:
   - Add FluentValidation (or manual validation) for all request DTOs
   - Return 400 with specific field errors: { "error": "Validation failed", "fields": { "title": "Title is required" } }

5. Rate Limiting:
   - Add ASP.NET Core rate limiting middleware
   - Limit: 100 requests per minute per API key
   - Return 429 with Retry-After header when exceeded

6. CORS:
   - Allow configured dashboard origins for MVP dev/self-hosted scenarios
   - Include local development origins explicitly (for example the Vite dev server)
```

**Acceptance Criteria:**
- [ ] Structured logs appear in console and log files
- [ ] API keys are not logged
- [ ] Unhandled exceptions return consistent error format
- [ ] /health returns database status and uptime
- [ ] Invalid request bodies return 400 with field-specific errors
- [ ] Rate limiting activates at 100 req/min per key

---

### Task 19 — PostgreSQL Support & Integration Tests
**Status:** Pending

**Goal:** Verify PostgreSQL works as an alternative provider, and add integration tests for critical paths.

**File Targets:**
Create these files:
- `tests/Hiveboard.Tests/Integration/TaskWorkflowTests.cs`
- `tests/Hiveboard.Tests/Integration/DependencyTests.cs`
- `tests/Hiveboard.Tests/Integration/AuthTests.cs`
- `tests/Hiveboard.Tests/Integration/TestFixture.cs`
- `tests/Hiveboard.Tests/Unit/TaskStateMachineTests.cs`

Update existing (if needed):
- `src/Hiveboard.Infrastructure/ServiceRegistration.cs`

**Agent Prompt:**
```
Status update requirement for this task:
- At the START of this task, update IMPLEMENTATION-GUIDE.md and set Task 19 status to Implementing.
- At the END (after acceptance criteria pass), set Task 19 status to Implemented.

Add integration tests and verify PostgreSQL support for Hiveboard.

1. Test Infrastructure (TestFixture.cs):
   - Use WebApplicationFactory<Program> for integration tests
   - Use EF Core InMemory or SQLite in-memory for test database
   - Create helper methods:
     - CreateAuthenticatedClient(apiKey) — returns HttpClient with API key header set
     - SeedTestData() — creates org, admin/coordinator credential context, optional orchestrator agent, and worker agents with known keys

2. Integration Tests — TaskWorkflowTests.cs:
   - Test the full happy path:
     a) Coordinator creates project
     b) Coordinator creates epic
     c) Coordinator creates tasks
     d) Coordinator adds dependencies
     e) Coordinator assigns task to worker
     f) Worker retrieves task with full context
     g) Worker transitions: assigned → in-progress → done
     h) System sends DependencyResolved notification
     i) Coordinator assigns next task

   - Test optional co-orchestrator compatibility:
     a) Optional orchestrator creates a project
     b) The project links that orchestrator
     c) Optional orchestrator can perform coordinator-side assignment / dependency actions
     d) Coordinator-facing notifications are also visible to that orchestrator when configured

   - Test blocked flow:
     a) Worker marks task as blocked
     b) Coordinator receives notification
     c) Coordinator unblocks → task back to assigned

   - Test decomposition flow:
     a) Worker decomposes task into subtasks
     b) Coordinator is notified
     c) Coordinator assigns subtasks
     d) All subtasks complete → parent auto-completes

3. DependencyTests.cs:
   - Test circular dependency detection (A→B→C→A)
   - Test self-dependency rejection
   - Test dependency enforcement (can't start task with unmet deps)

4. AuthTests.cs:
   - Test: no key → 401
   - Test: invalid key → 401
   - Test: admin/coordinator credential can access coordinator-first CRUD flows after organization scope resolution
   - Test: worker can't create tasks → 403
   - Test: worker can't assign tasks → 403
   - Test: worker can't transition other agent's tasks → 403
   - Test: coordinator-created project has no orchestrator attached by default
   - Test: optional orchestrator can still use coordinator-side endpoints when configured

5. Unit Tests — TaskStateMachineTests.cs:
   - Test every valid transition succeeds
   - Test every invalid transition fails with correct error
   - Test dependency check on in-progress transition

6. PostgreSQL verification:
   - Update ServiceRegistration.cs if needed to ensure PostgreSQL config works
   - Add a note in README about how to switch to PostgreSQL
   - Verify the EF Core model produces valid PostgreSQL migrations:
     dotnet ef migrations add PgTest --project src/Hiveboard.Infrastructure --startup-project src/Hiveboard.Api -- --DatabaseProvider postgresql
     (then delete this test migration)
```

**Acceptance Criteria:**
- [ ] All integration tests pass with `dotnet test`
- [ ] Full workflow test covers the complete coordinator + worker lifecycle
- [ ] Integration tests cover admin/coordinator organization-scope resolution
- [ ] Integration tests cover optional co-orchestrator compatibility
- [ ] Auth tests verify all permission boundaries
- [ ] Dependency cycle detection test passes
- [ ] State machine unit tests cover all transitions
- [ ] PostgreSQL provider can be configured and migrations work

---

## Post-Implementation Checklist

After all 19 tasks are complete, verify end-to-end:

- [ ] `dotnet build Hiveboard.sln` — clean build, no warnings
- [ ] `dotnet test` — all tests pass
- [ ] Start the API, run the seeder, verify dashboard loads in bundled mode or standalone mode
- [ ] Use curl/Postman to walk through the full coordinator + worker workflow from Appendix A of the PRD
- [ ] Open `/swagger` in Development and verify all REST endpoints are documented with `X-Api-Key` security
- [ ] Connect an MCP client and verify tool discovery
- [ ] Verify MCP tool/resource metadata quality (`list_tools`/`list_resources`) and structured error behavior
- [ ] Switch to PostgreSQL config, run migrations, verify it works
- [ ] Review logs — structured, no secrets leaked

---

## Appendix: Quick Reference

### Running the API
```bash
cd src/Hiveboard.Api
dotnet run
# API: http://localhost:5000
# Dashboard: http://localhost:5000/dashboard
# Health: http://localhost:5000/health
```

### Running the Dashboard (Development)
```bash
cd src/Hiveboard.Dashboard
npm run dev
# Dashboard (dev): http://localhost:5173
```

### Test API Keys (Development)
```
Orchestrator: dev-orchestrator-key-123
Worker: dev-worker-key-456
```

### Database Switch
```json
// appsettings.json — SQLite (default)
{ "DatabaseProvider": "sqlite", "ConnectionStrings": { "DefaultConnection": "Data Source=hiveboard.db" } }

// appsettings.json — PostgreSQL
{ "DatabaseProvider": "postgresql", "ConnectionStrings": { "DefaultConnection": "Host=localhost;Database=hiveboard;Username=hiveboard;Password=secret" } }
```
