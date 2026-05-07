using System.ComponentModel;
using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Hiveboard.Api.Mcp;

/// <summary>
/// MCP tools that wrap the Hiveboard application services.
/// Tool names are the public contract — they MUST match the PRD §5.4 table
/// and remain stable across patch releases. Renaming requires a versioning
/// strategy (a new tool name) per PRD §5.7.
///
/// Each tool takes its application service as a parameter so the SDK
/// resolves it from the per-request DI scope (the same scope the
/// <see cref="Hiveboard.Api.Auth.ApiKeyAuthHandler"/> populates with the
/// caller's <c>AgentContext</c>). That gives us tenant scoping, role
/// checks, and audit attribution for free.
///
/// Failures from the application services come back as <see cref="IResult"/>
/// values; we translate them to <see cref="McpException"/> with a
/// machine-readable error code so MCP clients see structured errors.
/// </summary>
[McpServerToolType]
public sealed class McpTools
{
    [McpServerTool(Name = "hiveboard_list_tasks")]
    [Description("List tasks for a project with optional filters. Maps to GET /api/v1/projects/{projectId}/tasks. " +
                 "Filters: status (backlog | assigned | in-progress | in-review | blocked | done), " +
                 "agentId (assigned worker), epicId (parent epic).")]
    public static async Task<string> ListTasks(
        TaskApplicationService taskService,
        [Description("The project ID (GUID) whose tasks should be listed.")] string projectId,
        [Description("Optional status filter.")] string? status = null,
        [Description("Optional assigned-worker agent ID (GUID) filter.")] string? agentId = null,
        [Description("Optional parent epic ID (GUID) filter.")] string? epicId = null)
    {
        var projectGuid = ParseRequiredGuid(projectId, nameof(projectId));
        var agentGuid = ParseOptionalGuid(agentId, nameof(agentId));
        var epicGuid = ParseOptionalGuid(epicId, nameof(epicId));

        var result = await taskService.ListProjectTasksAsync(projectGuid, status, agentGuid, epicGuid);
        return await McpResultConverter.ToJsonStringAsync(result);
    }

    [McpServerTool(Name = "hiveboard_get_task")]
    [Description("Get the full task context bundle (task, epic, parent, subtasks, dependencies, notes, events, decisions). " +
                 "Maps to GET /api/v1/tasks/{taskId}.")]
    public static async Task<string> GetTask(
        TaskApplicationService taskService,
        [Description("The task ID (GUID) to fetch.")] string taskId)
    {
        var taskGuid = ParseRequiredGuid(taskId, nameof(taskId));
        var result = await taskService.GetTaskByIdAsync(taskGuid);
        return await McpResultConverter.ToJsonStringAsync(result);
    }

    [McpServerTool(Name = "hiveboard_update_status")]
    [Description("Transition a task to a new status. Enforces the task state machine: " +
                 "blocked requires a blockedReason; assigned requires assignedAgentId. " +
                 "Maps to PATCH /api/v1/tasks/{taskId}/status.")]
    public static async Task<string> UpdateStatus(
        TaskApplicationService taskService,
        [Description("The task ID (GUID) whose status should change.")] string taskId,
        [Description("New status: backlog | assigned | in-progress | in-review | blocked | done.")] string status,
        [Description("Required when transitioning to assigned: the worker agent ID (GUID).")] string? assignedAgentId = null,
        [Description("Required when transitioning to blocked: human-readable reason.")] string? blockedReason = null)
    {
        var taskGuid = ParseRequiredGuid(taskId, nameof(taskId));
        var assignedGuid = ParseOptionalGuid(assignedAgentId, nameof(assignedAgentId));

        var request = new UpdateTaskStatusRequest(status, blockedReason, assignedGuid);
        var result = await taskService.UpdateTaskStatusAsync(taskGuid, request);
        return await McpResultConverter.ToJsonStringAsync(result);
    }

    [McpServerTool(Name = "hiveboard_add_note")]
    [Description("Add a note to a task. Note types: context | progress | review-request | blocker | resolution. " +
                 "Maps to POST /api/v1/tasks/{taskId}/notes.")]
    public static async Task<string> AddNote(
        NotesAndDecisionsApplicationService notesService,
        [Description("The task ID (GUID) the note belongs to.")] string taskId,
        [Description("Note content (required, non-empty).")] string content,
        [Description("Note type: context | progress | review-request | blocker | resolution.")] string noteType)
    {
        var taskGuid = ParseRequiredGuid(taskId, nameof(taskId));
        var request = new CreateNoteRequest(content, noteType);
        var result = await notesService.CreateTaskNoteAsync(taskGuid, request);
        return await McpResultConverter.ToJsonStringAsync(result);
    }

    [McpServerTool(Name = "hiveboard_decompose_task")]
    [Description("Break a task into 1–50 subtasks. Each subtask is created in 'backlog' status under the parent task. " +
                 "Maps to POST /api/v1/tasks/{taskId}/subtasks.")]
    public static async Task<string> DecomposeTask(
        TaskApplicationService taskService,
        [Description("The parent task ID (GUID) to decompose.")] string taskId,
        [Description("Subtasks to create. Each item needs a non-empty title; description is optional.")]
        DecomposeSubtaskInput[] subtasks)
    {
        var taskGuid = ParseRequiredGuid(taskId, nameof(taskId));

        if (subtasks is null || subtasks.Length == 0)
        {
            throw new McpException(
                "[invalid_argument] At least one subtask is required.");
        }

        var subtaskRequests = subtasks
            .Select(subtask => new DecomposeTaskSubtaskRequest(subtask.Title ?? string.Empty, subtask.Description))
            .ToList();

        var result = await taskService.DecomposeTaskAsync(taskGuid, new DecomposeTaskRequest(subtaskRequests));
        return await McpResultConverter.ToJsonStringAsync(result);
    }

    [McpServerTool(Name = "hiveboard_add_decision")]
    [Description("Record an architectural decision for a project. Optionally link it to a task. " +
                 "Status: proposed | accepted | superseded. Maps to POST /api/v1/projects/{projectId}/decisions.")]
    public static async Task<string> AddDecision(
        NotesAndDecisionsApplicationService notesService,
        [Description("The project ID (GUID) the decision belongs to.")] string projectId,
        [Description("Short title for the decision.")] string title,
        [Description("Decision content / rationale (markdown allowed).")] string content,
        [Description("Decision status: proposed | accepted | superseded.")] string status,
        [Description("Optional related task ID (GUID).")] string? taskId = null)
    {
        var projectGuid = ParseRequiredGuid(projectId, nameof(projectId));
        var taskGuid = ParseOptionalGuid(taskId, nameof(taskId));

        var request = new CreateDecisionRequest(title, content, taskGuid, status);
        var result = await notesService.CreateDecisionAsync(projectGuid, request);
        return await McpResultConverter.ToJsonStringAsync(result);
    }

    [McpServerTool(Name = "hiveboard_get_dependencies")]
    [Description("Get the dependency graph for a project (nodes = tasks, edges = blocks relationships). " +
                 "Maps to GET /api/v1/projects/{projectId}/dependencies/graph.")]
    public static async Task<string> GetDependencies(
        DependencyApplicationService dependencyService,
        [Description("The project ID (GUID) whose dependency graph should be returned.")] string projectId,
        CancellationToken cancellationToken = default)
    {
        var projectGuid = ParseRequiredGuid(projectId, nameof(projectId));
        var result = await dependencyService.GetDependencyGraphAsync(projectGuid, cancellationToken);
        return await McpResultConverter.ToJsonStringAsync(result);
    }

    [McpServerTool(Name = "hiveboard_my_tasks")]
    [Description("Return the calling agent's profile, assigned (non-done) tasks, and unread notification count. " +
                 "Uses the X-Api-Key from the request to identify the caller. Maps to GET /api/v1/agents/me.")]
    public static async Task<string> MyTasks(AgentApplicationService agentService)
    {
        var result = await agentService.GetCurrentAgentAsync();
        return await McpResultConverter.ToJsonStringAsync(result);
    }

    [McpServerTool(Name = "hiveboard_get_notifications")]
    [Description("List unacknowledged notifications for the calling agent. " +
                 "Maps to GET /api/v1/notifications.")]
    public static async Task<string> GetNotifications(NotificationApplicationService notificationService)
    {
        var result = await notificationService.ListMyNotificationsAsync();
        return await McpResultConverter.ToJsonStringAsync(result);
    }

    public sealed class DecomposeSubtaskInput
    {
        [Description("Subtask title (required, non-empty).")]
        public string? Title { get; set; }

        [Description("Optional subtask description.")]
        public string? Description { get; set; }
    }

    private static Guid ParseRequiredGuid(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new McpException(
                $"[invalid_argument] '{parameterName}' is required.");
        }

        if (!Guid.TryParse(value, out var parsed))
        {
            throw new McpException(
                $"[invalid_argument] '{parameterName}' must be a valid GUID.");
        }

        return parsed;
    }

    private static Guid? ParseOptionalGuid(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Guid.TryParse(value, out var parsed))
        {
            throw new McpException(
                $"[invalid_argument] '{parameterName}' must be a valid GUID when provided.");
        }

        return parsed;
    }
}
