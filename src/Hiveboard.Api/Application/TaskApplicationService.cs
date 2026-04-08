using Hiveboard.Api.Auth;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Api.Application;

public sealed class TaskApplicationService
{
    private const string CoordinatorAuditAgentName = "Coordinator";

    private readonly HiveboardDbContext _db;
    private readonly AgentContext _agentContext;
    private readonly IAgentAccessGuard _accessGuard;

    public TaskApplicationService(
        HiveboardDbContext db,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard)
    {
        _db = db;
        _agentContext = agentContext;
        _accessGuard = accessGuard;
    }

    public async Task<IResult> ListProjectTasksAsync(
        Guid projectId,
        string? status,
        Guid? agentId,
        Guid? epicId)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId);

        if (project is null)
            return Results.NotFound(new { error = "Project not found" });

        if (project.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Project belongs to a different organization");

        var tasksQuery = _db.AgentTasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseTaskStatus(status, out var parsedStatus))
                return Results.BadRequest(new { error = "Invalid status filter" });

            tasksQuery = tasksQuery.Where(task => task.Status == parsedStatus);
        }

        if (agentId.HasValue)
            tasksQuery = tasksQuery.Where(task => task.AssignedAgentId == agentId);

        if (epicId.HasValue)
            tasksQuery = tasksQuery.Where(task => task.EpicId == epicId);

        var tasks = await tasksQuery.ToListAsync();

        return Results.Ok(tasks
            .OrderBy(task => task.CreatedAt)
            .Select(ToTaskResponse));
    }

    public async Task<IResult> CreateTaskAsync(Guid projectId, CreateTaskRequest? request)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var coordinatorOrOrchestratorError = _accessGuard.ValidateCoordinatorOrOrchestratorScope(
            _agentContext,
            "Only coordinators or orchestrator agents can create tasks");
        if (coordinatorOrOrchestratorError is not null)
            return coordinatorOrOrchestratorError;

        if (request is null || string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(new { error = "Title is required" });

        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId);

        if (project is null)
            return Results.NotFound(new { error = "Project not found" });

        if (project.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Project belongs to a different organization");

        if (request.EpicId.HasValue)
        {
            var epic = await _db.Epics
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == request.EpicId.Value);

            if (epic is null)
                return Results.NotFound(new { error = "Epic not found" });

            if (epic.ProjectId != projectId)
                return Results.BadRequest(new { error = "Epic does not belong to the selected project" });
        }

        if (request.ParentTaskId.HasValue)
        {
            var parentTask = await _db.AgentTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == request.ParentTaskId.Value);

            if (parentTask is null)
                return Results.NotFound(new { error = "Parent task not found" });

            if (parentTask.ProjectId != projectId)
                return Results.BadRequest(new { error = "Parent task does not belong to the selected project" });
        }

        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            EpicId = request.EpicId,
            ParentTaskId = request.ParentTaskId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Status = TaskStatusEnum.Backlog,
            Metadata = request.Metadata is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(request.Metadata),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.AgentTasks.Add(task);
        await _db.SaveChangesAsync();

        return Results.CreatedAtRoute(
            "GetTaskById",
            new { id = task.Id },
            ToTaskResponse(task));
    }

    public async Task<IResult> GetTaskByIdAsync(Guid taskId)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var task = await _db.AgentTasks
            .AsNoTracking()
            .AsSplitQuery()
            .Include(candidate => candidate.Project)
            .Include(candidate => candidate.Epic)
            .Include(candidate => candidate.ParentTask)
            .Include(candidate => candidate.Subtasks)
            .Include(candidate => candidate.Dependencies)
                .ThenInclude(dependency => dependency.DependsOnTask)
            .Include(candidate => candidate.DependentTasks)
                .ThenInclude(dependency => dependency.Task)
            .Include(candidate => candidate.Notes)
                .ThenInclude(note => note.Agent)
            .Include(candidate => candidate.Events)
                .ThenInclude(taskEvent => taskEvent.Agent)
            .Include(candidate => candidate.Decisions)
                .ThenInclude(decision => decision.Agent)
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId);

        if (task is null)
            return Results.NotFound(new { error = "Task not found" });

        if (task.Project?.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Task belongs to a different organization");

        return Results.Ok(ToTaskDetailResponse(task));
    }

    public async Task<IResult> UpdateTaskAsync(Guid taskId, UpdateTaskRequest? request)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var coordinatorOrOrchestratorError = _accessGuard.ValidateCoordinatorOrOrchestratorScope(
            _agentContext,
            "Only coordinators or orchestrator agents can update tasks");
        if (coordinatorOrOrchestratorError is not null)
            return coordinatorOrOrchestratorError;

        if (request is null)
            return Results.BadRequest(new { error = "Request body is required" });

        var task = await _db.AgentTasks
            .Include(candidate => candidate.Project)
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId);

        if (task is null)
            return Results.NotFound(new { error = "Task not found" });

        if (task.Project?.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Task belongs to a different organization");

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return Results.BadRequest(new { error = "Title cannot be empty" });

            task.Title = request.Title.Trim();
        }

        if (request.Description is not null)
            task.Description = request.Description.Trim();

        if (request.EpicId.HasValue)
        {
            if (request.EpicId.Value == Guid.Empty)
            {
                task.EpicId = null;
            }
            else
            {
                var epic = await _db.Epics
                    .AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id == request.EpicId.Value);

                if (epic is null)
                    return Results.NotFound(new { error = "Epic not found" });

                if (epic.ProjectId != task.ProjectId)
                    return Results.BadRequest(new { error = "Epic does not belong to the task project" });

                task.EpicId = request.EpicId.Value;
            }
        }

        if (request.Metadata is not null)
            task.Metadata = new Dictionary<string, string>(request.Metadata);

        if (request.AssignedAgentId.HasValue)
        {
            if (request.AssignedAgentId.Value == Guid.Empty)
                return Results.BadRequest(new { error = "AssignedAgentId must be a valid value" });

            var requestedAssigneeId = request.AssignedAgentId.Value;

            if (task.AssignedAgentId.HasValue && task.AssignedAgentId.Value != requestedAssigneeId)
                return Results.Conflict(new { error = "Task is already assigned to a different agent" });

            if (task.AssignedAgentId != requestedAssigneeId)
            {
                var assignedAgent = await _db.Agents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id == requestedAssigneeId);

                if (assignedAgent is null)
                    return Results.BadRequest(new { error = "Assigned agent not found" });

                if (assignedAgent.OrganizationId != _agentContext.OrganizationId)
                    return Results.BadRequest(new { error = "Assigned agent belongs to a different organization" });

                if (assignedAgent.Type != AgentType.Worker)
                    return Results.BadRequest(new { error = "Assigned agent must be a worker" });

                var now = DateTimeOffset.UtcNow;

                task.AssignedAgentId = requestedAssigneeId;

                if (task.Status == TaskStatusEnum.Backlog)
                    task.Status = TaskStatusEnum.Assigned;

                var eventAgentId = _agentContext.IsCoordinator
                    ? await GetOrCreateCoordinatorAuditAgentIdAsync(now)
                    : _agentContext.AgentId;

                var assignmentEvent = new TaskEvent
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    AgentId = eventAgentId,
                    EventType = "assigned",
                    OldValue = null,
                    NewValue = requestedAssigneeId.ToString(),
                    Timestamp = now
                };

                _db.TaskEvents.Add(assignmentEvent);

                _db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    AgentId = requestedAssigneeId,
                    Type = NotificationType.TaskAssigned,
                    TaskId = task.Id,
                    Message = $"Task '{task.Title}' has been assigned to you",
                    IsAcknowledged = false,
                    CreatedAt = now
                });
            }
        }

        task.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Results.Ok(ToTaskResponse(task));
    }

    private static TaskResponse ToTaskResponse(AgentTask task) =>
        new(
            task.Id,
            task.Title,
            ToApiValue(task.Status),
            task.AssignedAgentId,
            task.EpicId,
            task.CreatedAt,
            task.UpdatedAt);

    private static TaskDetailResponse ToTaskDetailResponse(AgentTask task)
    {
        var blockedBy = task.Dependencies
            .Where(dependency => dependency.DependsOnTask is not null)
            .OrderBy(dependency => dependency.DependsOnTask!.CreatedAt)
            .Select(dependency => new TaskContextDependencyTaskResponse(
                dependency.DependsOnTaskId,
                dependency.DependsOnTask!.Title,
                ToApiValue(dependency.DependsOnTask.Status)))
            .ToList();

        var blocking = task.DependentTasks
            .Where(dependency => dependency.Task is not null)
            .OrderBy(dependency => dependency.Task!.CreatedAt)
            .Select(dependency => new TaskContextDependencyTaskResponse(
                dependency.TaskId,
                dependency.Task!.Title,
                ToApiValue(dependency.Task.Status)))
            .ToList();

        var subtasks = task.Subtasks
            .OrderBy(subtask => subtask.CreatedAt)
            .Select(subtask => new TaskContextSubtaskResponse(
                subtask.Id,
                subtask.Title,
                ToApiValue(subtask.Status),
                subtask.AssignedAgentId,
                subtask.UpdatedAt))
            .ToList();

        var notes = task.Notes
            .OrderBy(note => note.CreatedAt)
            .Select(note => new TaskContextNoteResponse(
                note.Agent?.Name ?? string.Empty,
                ToApiValue(note.NoteType),
                note.Content,
                note.CreatedAt))
            .ToList();

        var events = task.Events
            .OrderBy(taskEvent => taskEvent.Timestamp)
            .Select(taskEvent => new TaskContextEventResponse(
                taskEvent.Id,
                taskEvent.EventType,
                taskEvent.OldValue,
                taskEvent.NewValue,
                taskEvent.Agent?.Name ?? "Coordinator",
                taskEvent.Timestamp))
            .ToList();

        var decisions = task.Decisions
            .OrderBy(decision => decision.CreatedAt)
            .Select(decision => new TaskContextDecisionResponse(
                decision.Id,
                decision.Title,
                decision.Content,
                ToApiValue(decision.Status),
                decision.Agent?.Name ?? string.Empty,
                decision.CreatedAt))
            .ToList();

        return new TaskDetailResponse(
            new TaskContextTaskResponse(
                task.Id,
                task.ProjectId,
                task.EpicId,
                task.ParentTaskId,
                task.AssignedAgentId,
                task.Title,
                task.Description,
                ToApiValue(task.Status),
                task.BlockedReason,
                new Dictionary<string, string>(task.Metadata),
                task.CreatedAt,
                task.UpdatedAt),
            task.Epic is null
                ? null
                : new TaskContextEpicResponse(
                    task.Epic.Id,
                    task.Epic.Title,
                    ToApiValue(task.Epic.Status)),
            task.ParentTask is null
                ? null
                : new TaskContextParentTaskResponse(
                    task.ParentTask.Id,
                    task.ParentTask.Title,
                    ToApiValue(task.ParentTask.Status)),
            subtasks,
            new TaskContextDependenciesResponse(blockedBy, blocking),
            notes,
            events,
            decisions);
    }

    private static string ToApiValue(Enum value) =>
        value.ToString().ToLowerInvariant();

    private static bool TryParseTaskStatus(string status, out TaskStatusEnum parsedStatus)
    {
        var normalized = status
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim();

        return Enum.TryParse(normalized, ignoreCase: true, out parsedStatus);
    }

    private async Task<Guid> GetOrCreateCoordinatorAuditAgentIdAsync(DateTimeOffset now)
    {
        var existingAgentId = await _db.Agents
            .Where(agent =>
                agent.OrganizationId == _agentContext.OrganizationId &&
                agent.Name == CoordinatorAuditAgentName &&
                agent.Type == AgentType.Orchestrator &&
                agent.AgentPlatform == AgentPlatform.Custom &&
                agent.Status == AgentStatus.Inactive &&
                agent.ApiKeyHash == null)
            .Select(agent => (Guid?)agent.Id)
            .FirstOrDefaultAsync();

        if (existingAgentId.HasValue)
            return existingAgentId.Value;

        var coordinatorAuditAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = _agentContext.OrganizationId,
            Name = CoordinatorAuditAgentName,
            Type = AgentType.Orchestrator,
            AgentPlatform = AgentPlatform.Custom,
            ApiKeyHash = null!,
            Status = AgentStatus.Inactive,
            CreatedAt = now
        };

        _db.Agents.Add(coordinatorAuditAgent);
        return coordinatorAuditAgent.Id;
    }

    private static IResult Forbidden(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
}
