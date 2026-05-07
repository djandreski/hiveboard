# Hiveboard MCP — Smoke Test

The MCP server runs in-process with the REST API and exposes the
HTTP/SSE transport at `/mcp`. This walkthrough verifies that
`list_tools`, `list_resources`, a successful tool invocation, and a
structured-error invocation all work against a freshly started API.

## 1. Start the API

```bash
dotnet run --project src/Hiveboard.Api
```

The console will log a generated admin key on first run *or* honour the
`HIVEBOARD_ADMIN_KEY` environment variable if you set it (recommended).
The MCP endpoint is at `http://localhost:<port>/mcp`.

You can also set `HIVEBOARD_API_KEY` to a known agent or admin key. When
that env var is set, the server falls back to it for `/mcp` requests
that arrive without an `X-Api-Key` header (handy for stdio proxies and
local inspector sessions).

## 2. Connect with the MCP Inspector

```bash
npx @modelcontextprotocol/inspector
```

In the inspector:

* Transport type: **SSE**
* URL: `http://localhost:<port>/mcp`
* Authorization tab → Custom headers: `X-Api-Key: <your-key>`

## 3. `tools/list`

Expect 9 tools, all named `hiveboard_*`:

```text
hiveboard_list_tasks
hiveboard_get_task
hiveboard_update_status
hiveboard_add_note
hiveboard_decompose_task
hiveboard_add_decision
hiveboard_get_dependencies
hiveboard_my_tasks
hiveboard_get_notifications
```

Each tool advertises a description and a JSON-Schema input schema
generated from the C# parameter types and `[Description]` attributes.

## 4. `resources/list`

Expect 3 URI templates:

```text
hiveboard://project/{projectId}/overview
hiveboard://task/{taskId}/context
hiveboard://project/{projectId}/decisions
```

## 5. Successful tool call

Pick a known project ID (the dev seeder creates a sample project) and
invoke:

```json
{
  "name": "hiveboard_my_tasks",
  "arguments": {}
}
```

Expected: a 200-style payload with the calling agent's profile and
assigned-task summary.

## 6. Invalid-input error

Invoke `hiveboard_get_task` with a bad GUID:

```json
{
  "name": "hiveboard_get_task",
  "arguments": { "taskId": "not-a-guid" }
}
```

Expected: an MCP error whose message starts with the machine-readable
prefix `[invalid_argument]` so clients can branch on it. The four
prefixes in active use are:

| Prefix              | When it fires                                  |
|---------------------|------------------------------------------------|
| `[invalid_argument]`| 400-level validation, malformed GUIDs, etc.    |
| `[unauthorized]`    | Missing / invalid X-Api-Key                    |
| `[forbidden]`       | Cross-tenant or insufficient role              |
| `[not_found]`       | Entity not found in the caller's organization  |
| `[conflict]`        | State-machine, dependency, or assignment clash |

## 7. Resource read (optional)

```json
{
  "method": "resources/read",
  "params": { "uri": "hiveboard://project/<known-project-id>/overview" }
}
```

Expected: an `application/json` payload with `project`, `stats`, and
`activeAgents`. Reading a bogus URI returns a `[not_found]` error.
