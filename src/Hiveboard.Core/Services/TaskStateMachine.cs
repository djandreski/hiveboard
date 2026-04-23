using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Core.Services;

public class AgentContext
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public AgentType AgentType { get; set; }
    public Guid OrganizationId { get; set; }
    public string? OrganizationScopeError { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsCoordinator => IsAdmin;
    public bool HasOrganizationScope => OrganizationId != Guid.Empty;
    public bool IsOrchestrator => AgentId != Guid.Empty && AgentType == AgentType.Orchestrator;
}

public enum TaskTransitionFailureKind
{
    None,
    Forbidden,
    InvalidTransition,
    DependencyViolation
}

public sealed record UnmetTaskDependency(
    Guid TaskId,
    string Title,
    TaskStatusEnum Status);

public sealed record TaskTransitionValidationResult(
    bool IsSuccess,
    TaskTransitionFailureKind FailureKind,
    string? ErrorMessage,
    IReadOnlyList<UnmetTaskDependency> UnmetDependencies)
{
    public static TaskTransitionValidationResult Success() =>
        new(true, TaskTransitionFailureKind.None, null, Array.Empty<UnmetTaskDependency>());

    public static TaskTransitionValidationResult Forbidden(string errorMessage) =>
        new(false, TaskTransitionFailureKind.Forbidden, errorMessage, Array.Empty<UnmetTaskDependency>());

    public static TaskTransitionValidationResult Invalid(string errorMessage) =>
        new(false, TaskTransitionFailureKind.InvalidTransition, errorMessage, Array.Empty<UnmetTaskDependency>());

    public static TaskTransitionValidationResult DependencyFailure(
        string errorMessage,
        IReadOnlyList<UnmetTaskDependency> unmetDependencies) =>
        new(false, TaskTransitionFailureKind.DependencyViolation, errorMessage, unmetDependencies);
}

public sealed class TaskStateMachine
{
    public TaskTransitionValidationResult ValidateTransition(AgentTask task, TaskStatusEnum newStatus, AgentContext caller)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(caller);

        if (task.Status == newStatus)
            return TaskTransitionValidationResult.Invalid($"Task is already in '{ToApiValue(newStatus)}' status.");

        if (newStatus == TaskStatusEnum.Done && task.Subtasks.Count > 0)
        {
            return TaskTransitionValidationResult.Invalid(
                "Tasks with subtasks cannot be completed directly. Complete all subtasks instead.");
        }

        return (task.Status, newStatus) switch
        {
            (TaskStatusEnum.Backlog, TaskStatusEnum.Assigned) => ValidateAssignment(task, caller),
            (TaskStatusEnum.Assigned, TaskStatusEnum.InProgress) => ValidateStart(task, caller),
            (TaskStatusEnum.InProgress, TaskStatusEnum.InReview) => RequireAssignedAgent(task, caller, "request review"),
            (TaskStatusEnum.InProgress, TaskStatusEnum.Blocked) => ValidateBlocked(task, caller),
            (TaskStatusEnum.InProgress, TaskStatusEnum.Done) => RequireAssignedAgent(task, caller, "complete this task"),
            (TaskStatusEnum.InReview, TaskStatusEnum.InProgress) => RequireCoordinatorSideActor(task, caller, "send a task back to in-progress"),
            (TaskStatusEnum.InReview, TaskStatusEnum.Done) => RequireCoordinatorSideActor(task, caller, "approve a task"),
            (TaskStatusEnum.Blocked, TaskStatusEnum.Assigned) => ValidateUnblock(task, caller),
            (_, TaskStatusEnum.Backlog) => ValidateResetToBacklog(task, caller),
            _ => TaskTransitionValidationResult.Invalid(
                $"Transition from '{ToApiValue(task.Status)}' to '{ToApiValue(newStatus)}' is not allowed.")
        };
    }

    private static TaskTransitionValidationResult ValidateAssignment(AgentTask task, AgentContext caller)
    {
        var accessResult = RequireCoordinatorSideActor(task, caller, "assign a task");
        if (!accessResult.IsSuccess)
            return accessResult;

        if (!task.AssignedAgentId.HasValue || task.AssignedAgentId.Value == Guid.Empty)
            return TaskTransitionValidationResult.Invalid("Transitioning to 'assigned' requires an assigned agent.");

        return TaskTransitionValidationResult.Success();
    }

    private static TaskTransitionValidationResult ValidateStart(AgentTask task, AgentContext caller)
    {
        var accessResult = RequireAssignedAgent(task, caller, "start this task");
        if (!accessResult.IsSuccess)
            return accessResult;

        var unmetDependencies = task.Dependencies
            .Where(dependency => dependency.DependsOnTask is null || dependency.DependsOnTask.Status != TaskStatusEnum.Done)
            .Select(dependency => new UnmetTaskDependency(
                dependency.DependsOnTaskId,
                dependency.DependsOnTask?.Title ?? $"Task {dependency.DependsOnTaskId}",
                dependency.DependsOnTask?.Status ?? TaskStatusEnum.Backlog))
            .ToList();

        if (unmetDependencies.Count > 0)
        {
            return TaskTransitionValidationResult.DependencyFailure(
                "All blocking dependencies must be done before the task can move to 'in-progress'.",
                unmetDependencies);
        }

        return TaskTransitionValidationResult.Success();
    }

    private static TaskTransitionValidationResult ValidateBlocked(AgentTask task, AgentContext caller)
    {
        var accessResult = RequireAssignedAgent(task, caller, "mark this task as blocked");
        if (!accessResult.IsSuccess)
            return accessResult;

        if (string.IsNullOrWhiteSpace(task.BlockedReason))
            return TaskTransitionValidationResult.Invalid("Transitioning to 'blocked' requires a blocked reason.");

        return TaskTransitionValidationResult.Success();
    }

    private static TaskTransitionValidationResult ValidateUnblock(AgentTask task, AgentContext caller)
    {
        var accessResult = RequireCoordinatorSideActor(task, caller, "move a blocked task back to assigned");
        if (!accessResult.IsSuccess)
            return accessResult;

        if (!task.AssignedAgentId.HasValue || task.AssignedAgentId.Value == Guid.Empty)
            return TaskTransitionValidationResult.Invalid("Transitioning to 'assigned' requires an assigned agent.");

        if (!string.IsNullOrWhiteSpace(task.BlockedReason))
            return TaskTransitionValidationResult.Invalid("Transitioning to 'assigned' clears the blocked reason.");

        return TaskTransitionValidationResult.Success();
    }

    private static TaskTransitionValidationResult ValidateResetToBacklog(AgentTask task, AgentContext caller)
    {
        var accessResult = RequireCoordinatorSideActor(task, caller, "reset a task to backlog");
        if (!accessResult.IsSuccess)
            return accessResult;

        if (task.AssignedAgentId.HasValue)
            return TaskTransitionValidationResult.Invalid("Transitioning to 'backlog' must clear the assigned agent.");

        if (!string.IsNullOrWhiteSpace(task.BlockedReason))
            return TaskTransitionValidationResult.Invalid("Transitioning to 'backlog' must clear the blocked reason.");

        return TaskTransitionValidationResult.Success();
    }

    private static TaskTransitionValidationResult RequireAssignedAgent(AgentTask task, AgentContext caller, string action)
    {
        if (task.AssignedAgentId.HasValue && task.AssignedAgentId.Value == caller.AgentId)
            return TaskTransitionValidationResult.Success();

        return TaskTransitionValidationResult.Forbidden($"Only the assigned agent can {action}.");
    }

    private static TaskTransitionValidationResult RequireCoordinatorSideActor(AgentTask task, AgentContext caller, string action)
    {
        if (caller.IsCoordinator)
            return TaskTransitionValidationResult.Success();

        if (caller.IsOrchestrator &&
            task.Project is not null &&
            task.Project.OrchestratorAgentId.HasValue &&
            task.Project.OrchestratorAgentId.Value == caller.AgentId)
        {
            return TaskTransitionValidationResult.Success();
        }

        return TaskTransitionValidationResult.Forbidden(
            $"Only the coordinator or the configured orchestrator can {action}.");
    }

    private static string ToApiValue(TaskStatusEnum status) =>
        status.ToString().ToLowerInvariant();
}
