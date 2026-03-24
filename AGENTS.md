# AGENTS.md

This repository is a .NET 10 + React monorepo for Hiveboard. Use this file as the working guide for future agents.

## Source of Truth

- `PRD-Hiveboard.md` defines product requirements and domain rules.
- `IMPLEMENTATION-GUIDE.md` defines the current task sequence and acceptance criteria.
- `README.md` gives the high-level project summary and run commands.

## Repo Layout

- `src/Hiveboard.Api` is the ASP.NET Core host and API surface.
- `src/Hiveboard.Core` holds domain entities, enums, and core rules.
- `src/Hiveboard.Infrastructure` contains EF Core persistence, migrations, and provider setup.
- `src/Hiveboard.Dashboard` is the React + TypeScript + Vite frontend.
- `tests/Hiveboard.Tests` contains unit and integration tests.

## Working Rules

- Inspect existing code and docs before editing.
- Keep changes scoped to the requested task; do not overwrite unrelated user work.
- Prefer the existing structure and naming conventions already used in the repo.
- Always add tests for new functionality.
- When fixing bugs, always run the existing relevant tests (and add a regression test when behavior changes).
- If you touch the dashboard, keep frontend changes aligned with the existing Vite/React setup.

## Validation

- Run `dotnet restore Hiveboard.sln`, `dotnet build Hiveboard.sln`, and `dotnet test Hiveboard.sln` for backend changes.
- Run the appropriate `npm` commands in `src/Hiveboard.Dashboard` when frontend files change.

### Sandbox Notes (Codex Desktop)

- In this sandbox, `dotnet test Hiveboard.sln` can fail without surfacing useful errors.
- Use this backend validation sequence instead:
  - `dotnet restore Hiveboard.sln`
  - `dotnet build Hiveboard.sln -p:BuildDashboardAssets=false`
  - `dotnet test tests/Hiveboard.Tests/Hiveboard.Tests.csproj --no-restore -p:UseSandboxBuildWorkaround=true -p:BuildDashboardAssets=false`
- `UseSandboxBuildWorkaround=true` is required for stable test resolution in this environment.

## Reporting

- Summarize the files changed, commands run, and any blockers.
- Call out anything that could affect other agents working in parallel.
