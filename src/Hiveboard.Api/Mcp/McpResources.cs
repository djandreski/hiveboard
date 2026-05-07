using System.ComponentModel;
using System.Text.Json;
using Hiveboard.Api.Application;
using Hiveboard.Core.Enums;
using Hiveboard.Core.Services;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Api.Mcp;

/// <summary>
/// MCP resources for Hiveboard. URI templates are the public contract — they
/// MUST match the PRD §5.4 table and remain stable across patch releases.
///
/// Each resource is read-only. Authentication still flows through the
/// X-Api-Key header; the per-request DI scope provides
/// <see cref="AgentContext"/> and <see cref="IAgentAccessGuard"/> so that
/// tenant scoping and not-found vs forbidden semantics match the REST API.
/// All payloads are returned as <c>application/json</c> text resources.
/// </summary>
[McpServerResourceType]
public sealed class McpResources
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    [McpServerResource(
        UriTemplate = "hiveboard://project/{projectId}/overview",
        Name = "Project overview",
        MimeType = "application/json")]
    [Description("Project summary, task count breakdown by status, decision count, and active agents.")]
    public static async Task<ResourceContents> ProjectOverview(
        RequestContext<ReadResourceRequestParams> requestContext,
        HiveboardDbContext db,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard,
        string projectId,
        CancellationToken cancellationToken)
    {
        var projectGuid = ParseGuid(projectId, "projectId", requestContext);
        EnsureOrganizationScope(accessGuard, agentContext, requestContext);

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectGuid, cancellationToken);

        if (project is null)
            throw NotFound($"Project '{projectGuid}' was not found.", requestContext);

        if (project.OrganizationId != agentContext.OrganizationId)
            throw Forbidden("Project belongs to a different organization.", requestContext);

        var statusGroups = await db.AgentTasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectGuid)
            .GroupBy(task => task.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var taskCountsByStatus = Enum.GetValues<TaskStatusEnum>()
            .ToDictionary(
                status => ToApiValue(status),
                status => statusGroups.FirstOrDefault(group => group.Status == status)?.Count ?? 0);

        var totalTasks = taskCountsByStatus.Values.Sum();

        var decisionCount = await db.DecisionRecords
            .AsNoTracking()
            .CountAsync(decision => decision.ProjectId == projectGuid, cancellationToken);

        var activeAgents = await db.AgentTasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectGuid &&
                           task.AssignedAgentId.HasValue &&
                           task.Status != TaskStatusEnum.Done)
            .Select(task => task.AssignedAgent)
            .Where(agent => agent != null)
            .Select(agent => new
            {
                agent!.Id,
                agent.Name,
                Type = agent.Type.ToString().ToLowerInvariant(),
                Platform = agent.AgentPlatform.ToString().ToLowerInvariant()
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        var payload = new
        {
            project = new
            {
                id = project.Id,
                name = project.Name,
                description = string.IsNullOrWhiteSpace(project.Description) ? null : project.Description,
                status = project.Status.ToString().ToLowerInvariant(),
                createdAt = project.CreatedAt
            },
            stats = new
            {
                totalTasks,
                taskCountsByStatus,
                decisionCount,
                activeAgentCount = activeAgents.Count
            },
            activeAgents
        };

        return JsonResource(requestContext.Params?.Uri, payload);
    }

    [McpServerResource(
        UriTemplate = "hiveboard://task/{taskId}/context",
        Name = "Task context bundle",
        MimeType = "application/json")]
    [Description("Full task context: task, project, epic, parent, subtasks, dependencies, notes, events, and related decisions.")]
    public static async Task<ResourceContents> TaskContext(
        RequestContext<ReadResourceRequestParams> requestContext,
        TaskContextService taskContextService,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard,
        HiveboardDbContext db,
        string taskId,
        CancellationToken cancellationToken)
    {
        var taskGuid = ParseGuid(taskId, "taskId", requestContext);
        EnsureOrganizationScope(accessGuard, agentContext, requestContext);

        var organizationId = await db.AgentTasks
            .AsNoTracking()
            .Where(task => task.Id == taskGuid)
            .Select(task => (Guid?)task.Project!.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);

        if (organizationId is null)
            throw NotFound($"Task '{taskGuid}' was not found.", requestContext);

        if (organizationId.Value != agentContext.OrganizationId)
            throw Forbidden("Task belongs to a different organization.", requestContext);

        var context = await taskContextService.GetFullContextAsync(taskGuid, cancellationToken);
        if (context is null)
            throw NotFound($"Task '{taskGuid}' was not found.", requestContext);

        var payload = new
        {
            task = new
            {
                id = context.Task.Id,
                projectId = context.Task.ProjectId,
                epicId = context.Task.EpicId,
                parentTaskId = context.Task.ParentTaskId,
                assignedAgentId = context.Task.AssignedAgentId,
                title = context.Task.Title,
                description = context.Task.Description,
                status = ToApiValue(context.Task.Status),
                blockedReason = context.Task.BlockedReason,
                metadata = context.Task.Metadata,
                createdAt = context.Task.CreatedAt,
                updatedAt = context.Task.UpdatedAt
            },
            project = new { id = context.Project.Id, name = context.Project.Name },
            epic = context.Epic is null
                ? null
                : new
                {
                    id = context.Epic.Id,
                    title = context.Epic.Title,
                    description = context.Epic.Description,
                    status = ToApiValue(context.Epic.Status)
                },
            parentTask = context.ParentTask is null
                ? null
                : new
                {
                    id = context.ParentTask.Id,
                    title = context.ParentTask.Title,
                    status = ToApiValue(context.ParentTask.Status)
                },
            subtasks = context.Subtasks.Select(subtask => new
            {
                id = subtask.Id,
                title = subtask.Title,
                status = ToApiValue(subtask.Status),
                assignedAgentId = subtask.AssignedAgentId,
                assignedAgentName = subtask.AssignedAgentName,
                updatedAt = subtask.UpdatedAt
            }),
            dependencies = new
            {
                blockedBy = context.Dependencies.BlockedBy.Select(dependency => new
                {
                    taskId = dependency.TaskId,
                    title = dependency.Title,
                    status = ToApiValue(dependency.Status),
                    dependencyId = dependency.DepId
                }),
                blocking = context.Dependencies.Blocking.Select(dependency => new
                {
                    taskId = dependency.TaskId,
                    title = dependency.Title,
                    status = ToApiValue(dependency.Status),
                    dependencyId = dependency.DepId
                })
            },
            notes = context.Notes.Select(note => new
            {
                agentName = note.AgentName,
                agentType = ToApiValue(note.AgentType),
                noteType = ToApiValue(note.NoteType),
                content = note.Content,
                createdAt = note.CreatedAt
            }),
            events = context.Events.Select(taskEvent => new
            {
                id = taskEvent.Id,
                eventType = taskEvent.EventType,
                oldValue = taskEvent.OldValue,
                newValue = taskEvent.NewValue,
                agentName = taskEvent.AgentName,
                timestamp = taskEvent.Timestamp
            }),
            relatedDecisions = context.RelatedDecisions.Select(decision => new
            {
                id = decision.Id,
                title = decision.Title,
                content = decision.Content,
                status = ToApiValue(decision.Status),
                agentName = decision.AgentName,
                createdAt = decision.CreatedAt
            })
        };

        return JsonResource(requestContext.Params?.Uri, payload);
    }

    [McpServerResource(
        UriTemplate = "hiveboard://project/{projectId}/decisions",
        Name = "Project decisions",
        MimeType = "application/json")]
    [Description("All decision records for a project, newest first.")]
    public static async Task<ResourceContents> ProjectDecisions(
        RequestContext<ReadResourceRequestParams> requestContext,
        HiveboardDbContext db,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard,
        string projectId,
        CancellationToken cancellationToken)
    {
        var projectGuid = ParseGuid(projectId, "projectId", requestContext);
        EnsureOrganizationScope(accessGuard, agentContext, requestContext);

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectGuid, cancellationToken);

        if (project is null)
            throw NotFound($"Project '{projectGuid}' was not found.", requestContext);

        if (project.OrganizationId != agentContext.OrganizationId)
            throw Forbidden("Project belongs to a different organization.", requestContext);

        var decisions = await db.DecisionRecords
            .AsNoTracking()
            .Include(decision => decision.Agent)
            .Where(decision => decision.ProjectId == projectGuid)
            .OrderByDescending(decision => decision.CreatedAt)
            .ToListAsync(cancellationToken);

        var payload = new
        {
            projectId = project.Id,
            decisionCount = decisions.Count,
            decisions = decisions.Select(decision => new
            {
                id = decision.Id,
                projectId = decision.ProjectId,
                taskId = decision.TaskId,
                agentId = decision.AgentId,
                agentName = decision.Agent?.Name ?? string.Empty,
                agentType = ToApiValue(decision.Agent?.Type ?? AgentType.Orchestrator),
                title = decision.Title,
                content = decision.Content,
                status = ToApiValue(decision.Status),
                createdAt = decision.CreatedAt
            })
        };

        return JsonResource(requestContext.Params?.Uri, payload);
    }

    private static TextResourceContents JsonResource(string? uri, object payload) =>
        new()
        {
            Uri = uri ?? string.Empty,
            MimeType = "application/json",
            Text = JsonSerializer.Serialize(payload, JsonOptions)
        };

    private static Guid ParseGuid(string value, string parameter, RequestContext<ReadResourceRequestParams> requestContext)
    {
        if (!Guid.TryParse(value, out var parsed))
        {
            throw new McpException(
                $"[invalid_argument] Resource URI parameter '{parameter}' must be a valid GUID. " +
                $"Received '{value}' for {requestContext.Params?.Uri}.");
        }

        return parsed;
    }

    private static void EnsureOrganizationScope(
        IAgentAccessGuard accessGuard,
        AgentContext agentContext,
        RequestContext<ReadResourceRequestParams> requestContext)
    {
        var scopeError = accessGuard.ValidateOrganizationScope(agentContext);
        if (scopeError is null)
            return;

        var message = string.IsNullOrWhiteSpace(agentContext.OrganizationScopeError)
            ? "Authenticated caller is missing an organization scope."
            : agentContext.OrganizationScopeError;

        throw new McpException(
            $"[unauthorized] {message} Resource: {requestContext.Params?.Uri}.");
    }

    private static McpException NotFound(string message, RequestContext<ReadResourceRequestParams> requestContext) =>
        new($"[not_found] {message} Resource: {requestContext.Params?.Uri}.");

    private static McpException Forbidden(string message, RequestContext<ReadResourceRequestParams> requestContext) =>
        new($"[forbidden] {message} Resource: {requestContext.Params?.Uri}.");

    private static string ToApiValue(Enum value) => value.ToString().ToLowerInvariant();
}
