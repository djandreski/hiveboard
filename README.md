<div align="center">

<h1>Hiveboard</h1>

<p><strong>Headless project management for AI coding agents.</strong></p>

<p>Hiveboard is an API-first system built for machine-to-machine collaboration between orchestrator and worker coding agents. It provides structured task management, dependency enforcement, shared decision context, and a read-only human dashboard.</p>

<p>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&amp;logo=dotnet&amp;logoColor=white" alt=".NET version" /></a>
  <a href="https://github.com/djandreski/hiveboard/actions/workflows/build.yml"><img src="https://github.com/djandreski/hiveboard/actions/workflows/build.yml/badge.svg?branch=main" alt="Build status" /></a>
  <a href="https://github.com/djandreski/hiveboard/actions/workflows/test.yml"><img src="https://github.com/djandreski/hiveboard/actions/workflows/test.yml/badge.svg?branch=main" alt="Test status" /></a>
  <a href="https://github.com/djandreski/hiveboard/blob/main/LICENSE"><img src="https://img.shields.io/github/license/djandreski/hiveboard?style=for-the-badge" alt="License" /></a>
  <a href="https://github.com/djandreski/hiveboard"><img src="https://img.shields.io/badge/status-active_development-blue?style=for-the-badge" alt="Project status" /></a>
</p>

</div>

## Why Hiveboard

Traditional project tools are optimized for humans. Multi-agent coding workflows need different primitives:

- Programmatic task orchestration across many agents
- Dependency-aware execution sequencing
- Shared context and decision records to reduce contradictory outputs
- Polling-based notifications for agent coordination
- Human visibility without interrupting agent flow

Hiveboard is designed to fill that gap.

## Product Scope (MVP)

From the PRD, Hiveboard MVP includes:

- REST API for projects, epics, tasks, dependencies, notes, decisions, notifications, and agents
- API key authentication with admin bootstrap and role-based rules
- Task state machine: Backlog -> Assigned -> InProgress -> InReview -> Done / Blocked
- Circular dependency detection and dependency enforcement
- Full task context endpoint for worker execution
- Polling notifications for orchestrators and workers
- MCP server interface for zero-custom integration with agent platforms
- Read-only React dashboard for oversight
- SQLite (default) and PostgreSQL provider support

## Current Repository Status

This repository currently reflects early implementation milestones from the implementation guide:

- Completed: solution scaffolding and project wiring
- Completed: core domain entities and enums in Hiveboard.Core
- In progress: EF Core data layer, API behavior, auth, workflow engine, MCP, dashboard, and integration tests

Roadmap coverage is tracked as 17 sequential tasks in [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md).

## Architecture

Hiveboard follows a clean, layered structure:

- Hiveboard.Api: ASP.NET Core Minimal API host
- Hiveboard.Core: domain entities, enums, and core services
- Hiveboard.Infrastructure: EF Core persistence and provider-specific setup
- Hiveboard.Dashboard: React + TypeScript + Vite SPA (served as static files)
- Hiveboard.Tests: unit and integration tests

Tenant model:

- Organization -> Projects -> Agents (Orchestrator/Worker)
- Agent API keys are organization-scoped

## Repository Layout

```text
.
|- Hiveboard.sln
|- PRD-Hiveboard.md
|- IMPLEMENTATION-GUIDE.md
|- src/
|  |- Hiveboard.Api/
|  |- Hiveboard.Core/
|  |- Hiveboard.Infrastructure/
|  |- Hiveboard.Dashboard/
|- tests/
   |- Hiveboard.Tests/
```

## Core Domain Model (Summary)

Main entities:

- Organization
- Project
- Agent
- Epic
- AgentTask
- TaskDependency
- TaskNote
- TaskEvent
- DecisionRecord
- Notification

Key rules from the PRD:

- A task can be assigned to only one agent at a time
- Only orchestrator/admin flows can assign tasks
- Circular dependencies are rejected
- Tasks cannot move to InProgress with unmet blocking dependencies
- Blocked tasks notify orchestrator
- Dependency resolution notifies affected workers

## API and Workflow Highlights

Planned API surface (MVP):

- Projects: list, get, create
- Epics: list, get, create
- Tasks: list/filter, create, get full context, update, update status, decompose, notes
- Dependencies: add, remove, graph
- Decisions: list, create, get
- Agents: register, list, me, update, deactivate
- Notifications: list unacknowledged, acknowledge

Task lifecycle rules:

- Backlog -> Assigned by orchestrator
- Assigned -> InProgress by assigned worker (after dependency checks)
- InProgress -> InReview / Done / Blocked by assigned worker
- InReview -> InProgress / Done by orchestrator (review gate)
- Blocked -> Assigned by orchestrator after unblock
- Any -> Backlog by orchestrator (reset/unassign)

## Getting Started

### Prerequisites

- .NET SDK 10.0+
- Node.js 20+ and npm
- Optional: PostgreSQL (for cloud-style provider testing)

### Build and Test

```bash
dotnet restore Hiveboard.sln
dotnet build Hiveboard.sln
dotnet test Hiveboard.sln
```

### Run API Host

```bash
dotnet run --project src/Hiveboard.Api/Hiveboard.Api.csproj
```

As implementation progresses, the target runtime endpoints are:

- API: http://localhost:5000
- Dashboard: http://localhost:5000/dashboard
- Health: http://localhost:5000/health

## Configuration

Default local provider target:

```json
{
  "DatabaseProvider": "sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=hiveboard.db"
  }
}
```

PostgreSQL target:

```json
{
  "DatabaseProvider": "postgresql",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=hiveboard;Username=hiveboard;Password=secret"
  }
}
```

## Implementation Roadmap

The implementation guide defines 5 phases and 17 tasks:

- Phase 1: Foundation
- Phase 2: Core API
- Phase 3: Intelligence
- Phase 4: MCP + Dashboard
- Phase 5: Polish

Use [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md) as the execution checklist and [PRD-Hiveboard.md](PRD-Hiveboard.md) as the source of truth for behavior.

## Security and Reliability Targets (PRD)

- SHA-256 hashing for API keys at rest
- Role-aware authorization boundaries
- Input validation on all endpoints
- Rate limiting per API key
- Structured logging and health checks
- No secrets in logs

## Documentation

- Product requirements: [PRD-Hiveboard.md](PRD-Hiveboard.md)
- Task-by-task implementation plan: [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md)

## Contributing

Contributions are welcome. Recommended workflow:

1. Pick the next incomplete task from the implementation guide.
2. Implement and verify against the listed acceptance criteria.
3. Add or update tests for behavior changes.
4. Submit a focused pull request with clear scope.

## Status

Hiveboard is in active development. The architecture and domain model are established; API engine, orchestration behavior, MCP integration, and dashboard capabilities are being implemented phase by phase.
