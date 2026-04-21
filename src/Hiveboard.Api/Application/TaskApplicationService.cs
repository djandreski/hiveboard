using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Core.Services;
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
    private readonly TaskStateMachine _taskStateMachine;

    public TaskApplicationService(
        HiveboardDbContext db,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard,
        TaskStateMachine taskStateMachine)
    {
        _db = db;
        _agentContext = agentContext;
        _accessGuard = accessGuard;
        _taskStateMachine = taskStateMachine;
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

        var projectWriteAccessError = _accessGuard.ValidateCoordinatorOrConfiguredProjectOrchestratorScope(
            _agentContext,
            project.OrchestratorAgentId,
            "Only the coordinator or the project's configured orchestrator can create tasks");
        if (projectWriteAccessError is not null)
            return projectWriteAccessError;

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

        var projectWriteAccessError = _accessGuard.ValidateCoordinatorOrConfiguredProjectOrchestratorScope(
            _agentContext,
            task.Project?.OrchestratorAgentId,
            "Only the coordinator or the project's configured orchestrator can update tasks");
        if (projectWriteAccessError is not null)
            return projectWriteAccessError;

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
                var now = DateTimeOffset.UtcNow;
                var assignedAgentValidation = await ValidateAndApplyAssignedAgentAsync(task, requestedAssigneeId);
                if (assignedAgentValidation is not null)
                    return assignedAgentValidation;

                if (task.Status is TaskStatusEnum.Backlog or TaskStatusEnum.Blocked)
                {
                    task.BlockedReason = null;

                    var transitionValidation = _taskStateMachine.ValidateTransition(task, TaskStatusEnum.Assigned, _agentContext);
                    if (!transitionValidation.IsSuccess)
                        return CreateTransitionFailureResult(transitionValidation);

                    task.Status = TaskStatusEnum.Assigned;
                }

                var eventAgentId = await GetEventAgentIdAsync(now);

                _db.TaskEvents.Add(new TaskEvent
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    AgentId = eventAgentId,
                    EventType = "assigned",
                    OldValue = null,
                    NewValue = requestedAssigneeId.ToString(),
                    Timestamp = now
                });

                _db.Notifications.Add(CreateTaskAssignedNotification(task, requestedAssigneeId, now));
            }
        }

        task.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Results.Ok(ToTaskResponse(task));
    }

    public async Task<IResult> UpdateTaskStatusAsync(Guid taskId, UpdateTaskStatusRequest? request)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        if (request is null || string.IsNullOrWhiteSpace(request.Status))
            return Results.BadRequest(new { error = "Status is required" });

        if (!TryParseTaskStatus(request.Status, out var newStatus))
            return Results.BadRequest(new { error = "Invalid status value" });

        var task = await _db.AgentTasks
            .AsSplitQuery()
            .Include(candidate => candidate.Project)
            .Include(candidate => candidate.Dependencies)
                .ThenInclude(dependency => dependency.DependsOnTask)
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId);

        if (task is null)
            return Results.NotFound(new { error = "Task not found" });

        if (task.Project?.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Task belongs to a different organization");

        var previousStatus = task.Status;
        var previousAssignedAgentId = task.AssignedAgentId;

        switch (newStatus)
        {
            case TaskStatusEnum.Backlog:
                task.AssignedAgentId = null;
                task.BlockedReason = null;
                break;
            case TaskStatusEnum.Assigned:
                if (request.AssignedAgentId.HasValue)
                {
                    if (request.AssignedAgentId.Value == Guid.Empty)
                        return Results.BadRequest(new { error = "AssignedAgentId must be a valid value" });

                    var assignedAgentValidation = await ValidateAndApplyAssignedAgentAsync(task, request.AssignedAgentId.Value);
                    if (assignedAgentValidation is not null)
                        return assignedAgentValidation;
                }

                task.BlockedReason = null;
                break;
            case TaskStatusEnum.Blocked:
                task.BlockedReason = request.BlockedReason?.Trim();
                break;
            default:
                task.BlockedReason = null;
                break;
        }

        var transitionValidation = _taskStateMachine.ValidateTransition(task, newStatus, _agentContext);
        if (!transitionValidation.IsSuccess)
            return CreateTransitionFailureResult(transitionValidation);

        var now = DateTimeOffset.UtcNow;
        var eventAgentId = await GetEventAgentIdAsync(now);

        task.Status = newStatus;
        task.UpdatedAt = now;

        _db.TaskEvents.Add(CreateStatusChangeEvent(task.Id, eventAgentId, previousStatus, newStatus, now));

        if (newStatus == TaskStatusEnum.Assigned &&
            task.AssignedAgentId.HasValue &&
            (!previousAssignedAgentId.HasValue || task.AssignedAgentId.Value != previousAssignedAgentId.Value))
        {
            _db.Notifications.Add(CreateTaskAssignedNotification(task, task.AssignedAgentId.Value, now));
        }

        switch (newStatus)
        {
            case TaskStatusEnum.InReview:
                await CreateCoordinatorTransitionNotificationsAsync(
                    task,
                    NotificationType.ReviewRequested,
                    $"Task '{task.Title}' is ready for review.",
                    now);
                break;
            case TaskStatusEnum.Blocked:
                await CreateCoordinatorTransitionNotificationsAsync(
                    task,
                    NotificationType.TaskBlocked,
                    $"Task '{task.Title}' is blocked: {task.BlockedReason}",
                    now);
                break;
            case TaskStatusEnum.Done:
                await HandleCompletedTaskAsync(task, eventAgentId, now);
                break;
        }

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
                ToApiValue(dependency.DependsOnTask.Status),
                dependency.Id))
            .ToList();

        var blocking = task.DependentTasks
            .Where(dependency => dependency.Task is not null)
            .OrderBy(dependency => dependency.Task!.CreatedAt)
            .Select(dependency => new TaskContextDependencyTaskResponse(
                dependency.TaskId,
                dependency.Task!.Title,
                ToApiValue(dependency.Task.Status),
                dependency.Id))
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

    private async Task<IResult?> ValidateAndApplyAssignedAgentAsync(AgentTask task, Guid requestedAssigneeId)
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

        task.AssignedAgentId = requestedAssigneeId;
        return null;
    }

    private async Task<Guid> GetEventAgentIdAsync(DateTimeOffset now)
    {
        if (_agentContext.IsCoordinator)
            return await GetOrCreateCoordinatorAuditAgentIdAsync(now);

        return _agentContext.AgentId;
    }

    private static TaskEvent CreateStatusChangeEvent(
        Guid taskId,
        Guid agentId,
        TaskStatusEnum previousStatus,
        TaskStatusEnum newStatus,
        DateTimeOffset timestamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AgentId = agentId,
            EventType = "status_changed",
            OldValue = ToApiValue(previousStatus),
            NewValue = ToApiValue(newStatus),
            Timestamp = timestamp
        };

    private static Notification CreateTaskAssignedNotification(AgentTask task, Guid agentId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Type = NotificationType.TaskAssigned,
            TaskId = task.Id,
            Message = $"Task '{task.Title}' has been assigned to you",
            IsAcknowledged = false,
            CreatedAt = now
        };

    private async Task CreateCoordinatorTransitionNotificationsAsync(
        AgentTask task,
        NotificationType notificationType,
        string message,
        DateTimeOffset now)
    {
        var coordinatorAgentId = await GetOrCreateCoordinatorAuditAgentIdAsync(now);
        _db.Notifications.Add(CreateNotification(coordinatorAgentId, notificationType, task.Id, message, now));

        if (task.Project?.OrchestratorAgentId is Guid orchestratorAgentId &&
            orchestratorAgentId != coordinatorAgentId)
        {
            _db.Notifications.Add(CreateNotification(orchestratorAgentId, notificationType, task.Id, message, now));
        }
    }

    private async Task HandleCompletedTaskAsync(AgentTask task, Guid eventAgentId, DateTimeOffset now)
    {
        await NotifyResolvedDependentsAsync(task, now);
        await TryAutoCompleteParentTaskAsync(task.ParentTaskId, eventAgentId, now);
    }

    private async Task NotifyResolvedDependentsAsync(AgentTask completedTask, DateTimeOffset now)
    {
        var dependentTaskIds = await _db.TaskDependencies
            .Where(dependency => dependency.DependsOnTaskId == completedTask.Id)
            .Select(dependency => dependency.TaskId)
            .Distinct()
            .ToListAsync();

        if (dependentTaskIds.Count == 0)
            return;

        var dependentTasks = await _db.AgentTasks
            .Include(candidate => candidate.Dependencies)
                .ThenInclude(dependency => dependency.DependsOnTask)
            .Where(candidate =>
                dependentTaskIds.Contains(candidate.Id) &&
                candidate.AssignedAgentId.HasValue &&
                candidate.Status != TaskStatusEnum.Done)
            .ToListAsync();

        foreach (var dependentTask in dependentTasks)
        {
            var isUnblocked = dependentTask.Dependencies
                .All(dependency => dependency.DependsOnTask is not null && dependency.DependsOnTask.Status == TaskStatusEnum.Done);

            if (!isUnblocked || !dependentTask.AssignedAgentId.HasValue)
                continue;

            _db.Notifications.Add(CreateNotification(
                dependentTask.AssignedAgentId.Value,
                NotificationType.DependencyResolved,
                dependentTask.Id,
                $"Task '{dependentTask.Title}' is now unblocked because '{completedTask.Title}' is done.",
                now));
        }
    }

    private async Task TryAutoCompleteParentTaskAsync(Guid? parentTaskId, Guid eventAgentId, DateTimeOffset now)
    {
        if (!parentTaskId.HasValue)
            return;

        var parentTask = await _db.AgentTasks
            .Include(candidate => candidate.Subtasks)
            .FirstOrDefaultAsync(candidate => candidate.Id == parentTaskId.Value);

        if (parentTask is null || parentTask.Status == TaskStatusEnum.Done)
            return;

        if (parentTask.Subtasks.Count == 0 || parentTask.Subtasks.Any(subtask => subtask.Status != TaskStatusEnum.Done))
            return;

        var previousStatus = parentTask.Status;
        parentTask.Status = TaskStatusEnum.Done;
        parentTask.BlockedReason = null;
        parentTask.UpdatedAt = now;

        _db.TaskEvents.Add(CreateStatusChangeEvent(parentTask.Id, eventAgentId, previousStatus, TaskStatusEnum.Done, now));

        await NotifyResolvedDependentsAsync(parentTask, now);
        await TryAutoCompleteParentTaskAsync(parentTask.ParentTaskId, eventAgentId, now);
    }

    private static Notification CreateNotification(
        Guid agentId,
        NotificationType notificationType,
        Guid taskId,
        string message,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Type = notificationType,
            TaskId = taskId,
            Message = message,
            IsAcknowledged = false,
            CreatedAt = now
        };

    private static IResult CreateTransitionFailureResult(TaskTransitionValidationResult validation)
    {
        return validation.FailureKind switch
        {
            TaskTransitionFailureKind.Forbidden => Forbidden(validation.ErrorMessage ?? "Transition is not allowed"),
            TaskTransitionFailureKind.DependencyViolation => Results.Conflict(new
            {
                error = validation.ErrorMessage,
                unmetDependencies = validation.UnmetDependencies.Select(dependency => new
                {
                    dependency.TaskId,
                    dependency.Title,
                    Status = ToApiValue(dependency.Status)
                })
            }),
            _ => Results.Conflict(new { error = validation.ErrorMessage })
        };
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
            ApiKeyHash = null,
            Status = AgentStatus.Inactive,
            CreatedAt = now
        };

        _db.Agents.Add(coordinatorAuditAgent);
        return coordinatorAuditAgent.Id;
    }

    private static IResult Forbidden(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
}
