<div align="center">

# 🐝 Hiveboard: The Coordination Layer for AI Agents

**I am building a coordination layer in .NET for human-supervised, multi-agent software development.**

<p>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&amp;logo=dotnet&amp;logoColor=white" alt=".NET version" /></a>
  <a href="https://github.com/djandreski/hiveboard/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/djandreski/hiveboard/build.yml?branch=main&amp;style=for-the-badge&amp;label=build" alt="Build status" /></a>
  <a href="https://github.com/djandreski/hiveboard/actions/workflows/test.yml"><img src="https://img.shields.io/github/actions/workflow/status/djandreski/hiveboard/test.yml?branch=main&amp;style=for-the-badge&amp;label=test" alt="Test status" /></a>
  <a href="https://github.com/djandreski/hiveboard/blob/main/LICENSE"><img src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fapi.github.com%2Frepos%2Fdjandreski%2Fhiveboard%2Flicense&amp;query=%24.license.spdx_id&amp;label=license&amp;style=for-the-badge&amp;color=22c55e" alt="License" /></a>
  <a href="https://github.com/djandreski/hiveboard"><img src="https://img.shields.io/badge/status-active_development-blue?style=for-the-badge" alt="Project status" /></a>
</p>

</div>

## 🏗 Building in Public: The Roadmap

I am building Hiveboard in public with a **19-step Agentic Implementation Guide**.

My role is to define architecture, constraints, and acceptance criteria. AI coding agents execute the tasks. I review the output, push the standard up, and iterate.

**Current Progress: 74% Complete (14 of 19 tasks)**

- [x] **Task 1:** Project initialization and clean architecture
- [x] **Task 2:** Domain model (tasks, epics, agents)
- [x] **Task 3:** API foundation and dashboard scaffolding
- [x] **Task 4:** Database migrations and seed data (EF Core)
- [x] **Task 5:** Authentication and agent registration
- [x] **Task 6:** OpenAPI documentation and Swagger explorer
- [x] **Task 7:** Project and Epic CRUD endpoints
- [x] **Task 8:** Task CRUD and assignment endpoints
- [x] **Task 9:** Coordinator-first MVP realignment
- [x] **Task 10:** Task state machine
- [x] **Task 11:** Dependency management
- [x] **Task 12:** Task decomposition
- [x] **Task 13:** Notes and decision records endpoints
- [x] **Task 14:** Notification engine
- [ ] **Task 15:** Full task context assembly <- **WE ARE HERE**
- [ ] **Task 16-19:** Remaining coordination, MCP, dashboard, and polish tasks

Recent milestone:

- Hiveboard now runs in the coordinator-first MVP mode by default.
- The Admin API key acts as the self-hosted coordinator credential for control-plane CRUD.
- Optional orchestrator agents still work, but they are no longer required for normal project, epic, and task flows.
- Task dependency management now supports add/remove flows, circular dependency detection, and project graph retrieval.
- Task decomposition now supports worker/coordinator/orchestrator-triggered subtask creation, parent decomposition events, and coordinator/orchestrator notifications.
- Tasks now support shared notes across agents, and projects now expose decision records with task linking and status filtering.
- Notification engine centralizes creation behind a `NotificationService`, and agents (or the coordinator key) can poll and acknowledge their own notifications via `/api/v1/agents/me/notifications`.

See the full execution checklist in [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md).

## 🚀 The Vision

I started Hiveboard because I kept forcing AI agents through human-first PM tools. It was noisy, fragile, and full of UI overhead that agents do not need, but I also learned that taking humans completely out of the loop is the wrong starting point.

Traditional tools like Jira, Linear, and Trello are excellent for people. Agentic coding workflows need different primitives, but humans still need direct control over decomposition, assignment, review, and blocker resolution.

Hiveboard is my answer: a structured, API-first environment where humans coordinate and agents execute against shared state:

- **Sequence dependencies:** Ensure Agent B does not start until Agent A finishes
- **Share context:** Record architectural decisions in queryable decision logs
- **Stay in control:** Let humans intervene quickly when plans, assignments, or blockers need correction

## 🛠 The .NET Advantage

I chose **C# and .NET 10** on purpose.

The AI tooling world is crowded with Python-first products, but I wanted a stack that is predictable under pressure and ready for real production environments.

- **Type safety:** Strict contracts for human-to-agent and agent-to-agent coordination
- **Performance:** High-concurrency processing for orchestration-heavy workloads
- **Enterprise readiness:** Clean architecture, EF Core, and a dual-provider strategy (SQLite/PostgreSQL)

## 🤖 How I am Building This

Hiveboard is my working case study in **agentic software development**.

Every implementation task is executed by an AI coding agent following prompts from [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md) and behavior requirements from [PRD-Hiveboard.md](PRD-Hiveboard.md).

This repo is not just code; it is an operating model for how I build with agents. The MVP validates structured multi-agent coordination with humans in control first; autonomous orchestration comes later.

> **Insight from Task 4:** .NET migrations are a major advantage in agent workflows. They create an explicit, machine-readable history of schema changes that agents can safely extend with less relationship drift.

## 📦 MVP Scope

From the PRD, the first Hiveboard release includes:

- REST API for projects, epics, tasks, dependencies, notes, decisions, notifications, and agents
- API key authentication with admin bootstrap and role-aware authorization
- Task state machine: Backlog -> Assigned -> InProgress -> InReview -> Done / Blocked
- Circular dependency detection and dependency-aware execution gating
- Full task context endpoint for worker execution
- Polling notifications for coordinators and workers
- OpenAPI specification + Swagger UI for REST discoverability
- MCP server interface for zero-custom integration with agent platforms
- MCP discovery with stable tool/resource contracts and structured errors
- Human control plane for oversight, task coordination, review, and blocker resolution
- Optional co-orchestrator agent support on top of the same API
- SQLite (default) and PostgreSQL provider support

## 🧱 Architecture

I am keeping Hiveboard intentionally clean and layered from day one:

- **Hiveboard.Api:** ASP.NET Core Minimal API host
- **Hiveboard.Core:** Domain entities, enums, and core business rules
- **Hiveboard.Infrastructure:** EF Core persistence and provider-specific setup
- **Hiveboard.Dashboard:** React + TypeScript + Vite frontend app, developed independently and optionally bundled with the API for self-hosted deployments
- **Hiveboard.Tests:** Unit and integration tests

Coordination model:

- Organization -> Projects -> Agents (Worker + optional Orchestrator)
- Human coordinators operate through the dashboard/API
- Agent API keys are organization-scoped

## 🚦 Quick Start

### Prerequisites

- .NET SDK 10.0+
- Node.js 20+ and npm
- Optional: PostgreSQL for provider validation

### Build and Test

```bash
dotnet restore Hiveboard.sln
dotnet build Hiveboard.sln -p:BuildDashboardAssets=false
dotnet test Hiveboard.sln -p:BuildDashboardAssets=false
```

### Build the Dashboard

```bash
cd src/Hiveboard.Dashboard
npm install
npm run build
```

### Run the API (Automated Migrations + Seeding)

```bash
dotnet run --project src/Hiveboard.Api/Hiveboard.Api.csproj
```

### Run the Dashboard in Development

```bash
cd src/Hiveboard.Dashboard
npm run dev
```

Target runtime endpoints:

- API: http://localhost:5000
- Dashboard (bundled): http://localhost:5000/dashboard
- Dashboard (dev): http://localhost:5173
- Health: http://localhost:5000/health

## ⚙ Configuration

Default local provider:

```json
{
  "DatabaseProvider": "sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=hiveboard.db"
  }
}
```

PostgreSQL provider:

```json
{
  "DatabaseProvider": "postgresql",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=hiveboard;Username=hiveboard;Password=secret"
  }
}
```

## 🔒 Security and Reliability Targets

- SHA-256 hashing for API keys at rest
- Role-aware authorization boundaries
- Input validation on all endpoints
- Rate limiting per API key
- Structured logging and health checks
- No secrets in logs

## 📚 Documentation

- Product requirements: [PRD-Hiveboard.md](PRD-Hiveboard.md)
- Task-by-task implementation plan: [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md)

## 🤝 Contributing

1. Pick the next incomplete task from [IMPLEMENTATION-GUIDE.md](IMPLEMENTATION-GUIDE.md).
2. Implement against the acceptance criteria.
3. Add or update tests for behavior changes.
4. Submit a focused pull request.

## 📣 Follow the Build in Public Journey

I am documenting daily wins, architecture decisions, and agent failure modes on:

- X/Twitter: [https://x.com/djandreski](https://x.com/djandreski)
- LinkedIn: [https://www.linkedin.com/in/djandreski/](https://www.linkedin.com/in/djandreski/)
