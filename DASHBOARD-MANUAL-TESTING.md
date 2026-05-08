# Dashboard Manual Testing Guide (Task 17)

This guide walks through every acceptance criterion for the React dashboard.
Each test is independent — you can run them in any order, but the **Setup**
section must run first.

---

## Setup (one time)

### 1. Pick a known admin key

The auto-generated admin key is never written to logs in plaintext, so set a
known key via env var before the API starts. Pick anything memorable for the
session — it is just a test key.

```powershell
$env:HIVEBOARD_ADMIN_KEY = "hb_admin_test_$(New-Guid)"
$env:HIVEBOARD_ADMIN_KEY    # show it so you can paste it later
```

(Bash equivalent: `export HIVEBOARD_ADMIN_KEY="hb_admin_test_$(uuidgen)"; echo $HIVEBOARD_ADMIN_KEY`.)

### 2. Build everything

```powershell
# from repo root
dotnet build src/Hiveboard.Api/Hiveboard.Api.csproj

cd src/Hiveboard.Dashboard
npm install
npm run build              # standalone build
npm run build:bundle       # bundle for /dashboard
cd ../..
```

### 3. Start the API

```powershell
dotnet run --project src/Hiveboard.Api/Hiveboard.Api.csproj --urls=http://localhost:5099
```

The dev seed creates `Sample Project`, `Worker-1` (ClaudeCode),
`Worker-2` (Codex), and `Sample Orchestrator` (Custom). Leave the API
running in this terminal for every test below.

> **Tip:** keep `http://localhost:5099/swagger` open in a second tab — handy
> for creating a few extra projects/tasks via REST when you need more data.

---

## A. Build (AC: `npm run build` succeeds)

✅ **Pass** if Step 2 above produced `src/Hiveboard.Dashboard/dist/index.html`
and `src/Hiveboard.Dashboard/dist-bundle/index.html` with no errors.

```powershell
ls src/Hiveboard.Dashboard/dist/index.html, src/Hiveboard.Dashboard/dist-bundle/index.html
```

---

## B. Standalone dev mode (AC: dev mode + auth gate + session storage)

1. In a second terminal, start the dev server:

   ```powershell
   cd src/Hiveboard.Dashboard
   npm run dev
   ```

2. Open `http://localhost:5173/`.

3. **Expect:** the **Connect to Hiveboard** auth gate, with a single password
   input.

4. Open DevTools → Application → Storage. Note that `localStorage` is empty
   and there are no cookies for the site.

5. Paste the admin key from Setup → Connect.

6. **Expect:** the gate disappears within ~1s and the **Projects** page loads.

7. In DevTools → Application → **Session Storage**, confirm a single entry
   `hiveboard.apiKey` is set, and **localStorage / cookies remain empty**.

8. Hard-reload the page (Ctrl+F5). The dashboard should load without
   re-prompting (still in same tab session).

9. Close the tab, open a new tab to `http://localhost:5173/`. **Expect:**
   the auth gate reappears (session storage is per-tab).

---

## C. Bundled mode at `/dashboard` (AC: bundled mode + client-side routing)

1. Stop and restart the API with the dashboard bundle copied in:

   ```powershell
   # Ctrl+C the API, then:
   dotnet build src/Hiveboard.Api/Hiveboard.Api.csproj -p:BuildDashboardAssets=true
   dotnet run --project src/Hiveboard.Api/Hiveboard.Api.csproj --urls=http://localhost:5099
   ```

2. Open `http://localhost:5099/dashboard` (no trailing slash).

3. **Expect:** auth gate loads, request URL is `/dashboard`, response is
   HTML, status 200.

4. Sign in with the admin key.

5. Visit a deep link directly: `http://localhost:5099/dashboard/projects/<any-guid>/board`.

6. **Expect:** the SPA shell loads (no 404), the auth gate is bypassed
   (session-storage persists in same tab), and React Router renders the
   board page (or a "load failed" error if the GUID isn't a real project).

7. In DevTools → Network, hard-reload. Confirm the assets under
   `/dashboard/assets/index-*.js` and `/dashboard/assets/index-*.css`
   load with 200.

8. Verify the API still gates `/api/v1/*`:

   ```powershell
   curl -i http://localhost:5099/api/v1/projects   # → 401 without X-Api-Key
   ```

---

## D. Project Overview (AC: shows projects with stats)

Open the **Projects** page (`/`).

1. **Expect:** a card for **Sample Project** with:
   - Project name and description
   - Completion percentage and `0/0 tasks done` (no tasks yet)
   - A row of status pills (`Backlog: 0`, `Assigned: 0`, …)
   - `0 agents online · 0 assigned`

2. Click **New project**, enter `Manual Test Project` + a description, save.

3. **Expect:** the modal closes and a second card appears within ~10s
   (auto-refresh) or sooner via the immediate refetch.

---

## E. Task Board kanban (AC: kanban columns + create/edit/assign/reassign + blocker highlight)

1. Click into **Sample Project** → Board.

2. **Expect:** six columns, in order: **Backlog | Assigned | In Progress |
   In Review | Done | Blocked**, each empty.

3. Click **New task**, enter `Test Task A` with description `seed`. Save.

4. **Expect:** card appears in **Backlog** column. The card shows the
   title, "Unassigned", and `just now`.

5. Click the card. In the modal:
   a. Click **Edit / Reassign**, change Assigned agent to **Worker-1**, save.
   - **Expect:** modal returns to read-only with `Assigned to: Worker-1`,
     and the card moves to **Assigned**.
   b. Click **In Progress** under "Move to".
   - **Expect:** the badge updates to `In Progress` and the card moves to
     the **In Progress** column.
   c. In the **Mark blocked** panel, type `Waiting on database migration`
     and click **Block**.
   - **Expect:** modal updates to show a red `Blocked` badge with the
     reason, and the card moves to the **Blocked** column.

6. Close the modal and confirm the card in **Blocked** has a red/orange
   border and a faint red ring (the blocked column header is also tinted
   red).

7. Reopen the card. Click **In Progress** to unblock — verify the
   blocked-reason banner disappears and the card returns to In Progress.

8. **Reassign:** open card → Edit / Reassign → switch to **Worker-2** →
   Save. Card stays in In Progress, Assignee updates to Worker-2.

> Leave **Test Task A** in **In Progress** and **Worker-2** as assignee for
> later tests.

---

## F. Auto-refresh (AC: new data appears within 10s)

1. Stay on the Task Board for **Sample Project**.

2. In a new terminal, create a task via the API (replace `<PROJECT_ID>`
   with the Sample Project GUID; copy from the URL on the board page):

   ```powershell
   $h = @{ "X-Api-Key" = $env:HIVEBOARD_ADMIN_KEY }
   $body = @{ title = "Polled Task"; description = "auto-refresh test" } | ConvertTo-Json
   Invoke-RestMethod -Method POST -Uri "http://localhost:5099/api/v1/projects/<PROJECT_ID>/tasks" `
     -Headers $h -ContentType "application/json" -Body $body
   ```

3. **Expect:** within **≤10 seconds**, a `Polled Task` card appears in the
   **Backlog** column without any user action.

(The "auto-refreshing every 10s" caption under the page title is the same
hook driving every page; verifying it on the board is sufficient.)

---

## G. Agent Activity (AC: agent list with status, online/offline)

1. Click **Agents** in the top nav.

2. **Expect:** a table with at least three rows: `Worker-1`, `Worker-2`,
   `Sample Orchestrator`.

3. Each row shows: status badge (`○ Offline` for fresh seed agents),
   capitalized type (`worker` / `orchestrator`), platform, current task,
   and last seen (`—` for never).

4. Verify online detection by simulating an agent ping (updates
   `last_seen_at`):

   ```powershell
   # Worker-1 plaintext key from the dev seed:
   $w1 = @{ "X-Api-Key" = "dev-worker-key-123" }
   Invoke-RestMethod -Uri http://localhost:5099/api/v1/agents/me -Headers $w1 | Out-Null
   ```

5. Wait up to 10s. **Expect:** Worker-1's row reorders to the top with a
   green `● Online` badge and `Last seen: just now`. Its **Current task**
   column shows the In-Progress task assigned to it during E (if you left
   one assigned to Worker-1; otherwise it shows `Idle`).

6. Wait >5 minutes (or skip this aging check). **Expect:** Worker-1
   eventually flips back to `○ Offline`.

---

## H. Event Timeline (AC: chronological feed with old → new)

1. From the project nav (sub-nav under the header), click **Timeline**.

2. **Expect:** entries for the activities performed in section E:
   `task_created`, `task_assigned`, `task_status_changed` (with values
   like `backlog → assigned`, `assigned → inprogress`, `inprogress →
   blocked`, etc.), each showing relative timestamp, agent name, task
   title, and old → new transition.

3. The newest event is at the top. Hover the timestamp → tooltip shows
   absolute time.

---

## I. Decision Log (AC: markdown rendering)

1. Create a markdown decision via the API (replace `<PROJECT_ID>`):

   ```powershell
   $h = @{ "X-Api-Key" = $env:HIVEBOARD_ADMIN_KEY }
   $body = @"
   { "title": "Use SQLite for dev",
     "content": "## Context\n\nDev needs zero-setup persistence.\n\n## Decision\n\n- **SQLite** for local\n- PostgreSQL via provider switch\n\n```json\n{ \"DatabaseProvider\": \"sqlite\" }\n```",
     "status": "Accepted" }
   "@
   Invoke-RestMethod -Method POST -Uri "http://localhost:5099/api/v1/projects/<PROJECT_ID>/decisions" `
     -Headers $h -ContentType "application/json" -Body $body
   ```

2. In the dashboard, click the project **Decisions** tab.

3. **Expect:** a card with the decision title, an `Accepted` badge, the
   author, and an absolute date.

4. Click the card to expand.

5. **Expect:** rendered markdown — `## Context` and `## Decision` are
   styled headings, the bullet list renders as a `<ul>`, **SQLite** is
   bold, and the JSON snippet renders as a code block (monospace,
   bordered).

---

## J. Coordinator Console (AC: approve/send-back + blocker resolution + assignment gaps)

The console reads three buckets: **Awaiting review**, **Blocked tasks**, and
**Assignment gaps**.

### J.1 Approve / send-back from In Review

1. On the board, set up an "in review" task:
   - Click `Test Task A` → Move to **In Review**.

2. Go to project **Console**.

3. **Expect:** `Test Task A` appears under **Awaiting review** with the
   submitting agent's name.

4. Click **Send back to In Progress**.

5. **Expect:** the row disappears (≤10s); on the board, the card is back
   in **In Progress**.

6. From the board, move it back to **In Review** again, return to the
   Console, and click **Approve & mark done**.

7. **Expect:** card moves to **Done** on the board.

### J.2 Blocker resolution

1. From the board, mark a task **Blocked** (with a reason).

2. Open the **Console**. **Expect:** the task appears under **Blocked
   tasks** with the reason in red.

3. Try **Unblock (keep assignee)** → task returns to In Progress with the
   same assignee.

4. Block another task. In the Console, change the assignee dropdown to a
   different agent and click **Reassign & unblock**.

5. **Expect:** task returns to In Progress with the new assignee
   (verify on the board / via the agent activity page).

### J.3 Assignment gaps

1. Create a fresh task with no assignee:

   ```powershell
   $body = @{ title = "Unassigned Task" } | ConvertTo-Json
   Invoke-RestMethod -Method POST -Uri "http://localhost:5099/api/v1/projects/<PROJECT_ID>/tasks" `
     -Headers $h -ContentType "application/json" -Body $body
   ```

2. Open the **Console**. **Expect:** `Unassigned Task` appears under
   **Assignment gaps**.

3. Pick **Worker-1** in the dropdown and click **Assign**.

4. **Expect:** the row disappears within ≤10s; on the board, the card is
   in **Assigned** with Worker-1.

---

## K. Admin Panel — admin key (AC: metadata + rotation + show-once)

1. Click **Admin** in the top nav.

2. **Expect:** an `Admin key` card showing:
   - **Prefix:** the first 12 chars of the key followed by `…`
   - **Created:** a relative timestamp (matches when API was first started)
   - **Last used:** `just now` or seconds ago (the dashboard uses the
     admin key for every poll)

3. Click **Rotate admin key** → **Rotate now** in the confirmation modal.

4. **Expect:** a "New admin API key" modal opens, showing:
   - An amber warning banner
   - The new plaintext key in a monospace block
   - A `Copy to clipboard` button (verify it copies)
   - An `I saved it` button

5. Copy the new key, then click `I saved it`.

6. **Expect:** the metadata card refreshes (new prefix, `Created: just
   now`).

7. Verify the **old** key is rejected:

   ```powershell
   curl -i -H "X-Api-Key: $env:HIVEBOARD_ADMIN_KEY" http://localhost:5099/api/v1/admin/keys/info
   # → 401
   ```

8. Update your env var to the new key and re-run — should return 200:

   ```powershell
   $env:HIVEBOARD_ADMIN_KEY = "<new-key-from-modal>"
   curl -i -H "X-Api-Key: $env:HIVEBOARD_ADMIN_KEY" http://localhost:5099/api/v1/admin/keys/info
   ```

---

## L. Admin Panel — agent key rotation (AC: per-agent show-once)

1. On the Admin page, scroll to **Agent keys**. Find `Worker-1`.

2. Click **Rotate key** → **Rotate now**.

3. **Expect:** "New API key for Worker-1" modal with the same show-once
   layout as in K.

4. Copy the new key.

5. Verify the old worker key is rejected:

   ```powershell
   curl -i -H "X-Api-Key: dev-worker-key-123" http://localhost:5099/api/v1/agents/me
   # → 401
   ```

6. Verify the new worker key works:

   ```powershell
   curl -i -H "X-Api-Key: <new-worker-key>" http://localhost:5099/api/v1/agents/me
   # → 200
   ```

---

## M. Invalid / expired admin key re-prompt (AC: auth error → re-enter)

1. With the dashboard signed in, open DevTools → Application → Session
   Storage. Right-click `hiveboard.apiKey` → Edit, set to
   `not-a-real-key`. Reload the page.

2. **Expect:** the auth gate reappears with the error
   `That key was rejected. Please re-enter your coordinator/admin key.`,
   and session storage no longer contains `hiveboard.apiKey`.

3. Paste a valid key and connect.

4. **Expect:** dashboard loads normally.

5. As an additional check: while signed in, rotate the admin key from
   the Admin Panel (section K) but skip step 8 — instead, navigate to
   **Projects**.

6. **Expect:** within ≤10s the Projects page surfaces a 401 error
   banner (the SPA's polling hits the API with the now-invalidated
   stored key). Sign-out and sign back in with the new key clears it.

---

## N. Sign out (AC: session-only credential)

1. Click **Sign out** in the top-right of the header.

2. **Expect:** the auth gate reappears immediately and
   `sessionStorage.hiveboard.apiKey` is cleared (verify in DevTools).

---

## Acceptance criteria checklist

| Criterion | Section |
| --- | --- |
| `npm run build` succeeds | A |
| Dashboard works in standalone dev mode | B |
| Bundled mode works at `/dashboard` | C |
| Project overview shows projects + stats | D |
| Task board kanban with correct cards | E |
| Coordinators can create / edit / assign / reassign | E |
| Blocked column highlighted in red/orange | E |
| Auto-refresh within 10s | F |
| Agent activity list with status | G |
| Online/offline based on `last_seen_at >5 min` | G |
| Event timeline with chronological events | H |
| Decision log renders markdown | I |
| Coordinator Console: approve / send-back | J.1 |
| Coordinator Console: blocker resolution | J.2 |
| Coordinator Console: assignment gaps | J.3 |
| Client-side routing in standalone + bundled mode | B + C |
| Admin Panel prompts for key, stored in session storage | B + C |
| Admin Panel shows admin key metadata | K |
| Admin key rotation — shown once | K |
| Agent key rotation — shown once | L |
| Invalid/expired admin key prompts re-entry | M |

---

## Cleanup

```powershell
# stop the dev server (Ctrl+C in the npm run dev terminal)
# stop the API     (Ctrl+C in the dotnet run terminal)
# optional: reset the local SQLite seed data
Remove-Item src/Hiveboard.Api/hiveboard.db, src/Hiveboard.Api/hiveboard.db-shm, src/Hiveboard.Api/hiveboard.db-wal -ErrorAction SilentlyContinue
```
