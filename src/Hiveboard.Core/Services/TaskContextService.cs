using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Core.Services;

public interface ITaskContextRepository
{
    Task<AgentTask?> GetTaskWithFullContextAsync(Guid taskId, CancellationToken cancellationToken = default);
}

public sealed record TaskContext(
    TaskContextTask Task,
    TaskContextProject Project,
    TaskContextEpic? Epic,
    TaskContextParentTask? ParentTask,
    IReadOnlyList<TaskContextSubtask> Subtasks,
    TaskContextDependencies Dependencies,
    IReadOnlyList<TaskContextNote> Notes,
    IReadOnlyList<TaskContextEvent> Events,
    IReadOnlyList<TaskContextDecision> RelatedDecisions);

public sealed record TaskContextTask(
    Guid Id,
    Guid ProjectId,
    Guid? EpicId,
    Guid? ParentTaskId,
    Guid? AssignedAgentId,
    string Title,
    string Description,
    TaskStatusEnum Status,
    string? BlockedReason,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TaskContextProject(Guid Id, string Name);

public sealed record TaskContextEpic(
    Guid Id,
    string Title,
    string Description,
    EpicStatus Status);

public sealed record TaskContextParentTask(Guid Id, string Title, TaskStatusEnum Status);

public sealed record TaskContextSubtask(
    Guid Id,
    string Title,
    TaskStatusEnum Status,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    DateTimeOffset UpdatedAt);

public sealed record TaskContextDependencies(
    IReadOnlyList<TaskContextDependencyTask> BlockedBy,
    IReadOnlyList<TaskContextDependencyTask> Blocking);

public sealed record TaskContextDependencyTask(
    Guid TaskId,
    string Title,
    TaskStatusEnum Status,
    Guid DepId);

public sealed record TaskContextNote(
    string AgentName,
    AgentType AgentType,
    NoteType NoteType,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record TaskContextEvent(
    Guid Id,
    string EventType,
    string? OldValue,
    string? NewValue,
    string AgentName,
    DateTimeOffset Timestamp);

public sealed record TaskContextDecision(
    Guid Id,
    string Title,
    string Content,
    DecisionStatus Status,
    string AgentName,
    DateTimeOffset CreatedAt);

public sealed class TaskContextService
{
    private readonly ITaskContextRepository _repository;

    public TaskContextService(ITaskContextRepository repository)
    {
        _repository = repository;
    }

    public async Task<TaskContext?> GetFullContextAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _repository.GetTaskWithFullContextAsync(taskId, cancellationToken);
        return task is null ? null : Project(task);
    }

    private static TaskContext Project(AgentTask task)
    {
        var blockedBy = task.Dependencies
            .Where(dependency => dependency.DependsOnTask is not null)
            .OrderBy(dependency => dependency.DependsOnTask!.CreatedAt)
            .Select(dependency => new TaskContextDependencyTask(
                dependency.DependsOnTaskId,
                dependency.DependsOnTask!.Title,
                dependency.DependsOnTask.Status,
                dependency.Id))
            .ToList();

        var blocking = task.DependentTasks
            .Where(dependency => dependency.Task is not null)
            .OrderBy(dependency => dependency.Task!.CreatedAt)
            .Select(dependency => new TaskContextDependencyTask(
                dependency.TaskId,
                dependency.Task!.Title,
                dependency.Task.Status,
                dependency.Id))
            .ToList();

        var subtasks = task.Subtasks
            .OrderBy(subtask => subtask.CreatedAt)
            .Select(subtask => new TaskContextSubtask(
                subtask.Id,
                subtask.Title,
                subtask.Status,
                subtask.AssignedAgentId,
                subtask.AssignedAgent?.Name,
                subtask.UpdatedAt))
            .ToList();

        var notes = task.Notes
            .OrderBy(note => note.CreatedAt)
            .Select(note => new TaskContextNote(
                note.Agent?.Name ?? string.Empty,
                note.Agent?.Type ?? AgentType.Orchestrator,
                note.NoteType,
                note.Content,
                note.CreatedAt))
            .ToList();

        var events = task.Events
            .OrderBy(taskEvent => taskEvent.Timestamp)
            .Select(taskEvent => new TaskContextEvent(
                taskEvent.Id,
                taskEvent.EventType,
                taskEvent.OldValue,
                taskEvent.NewValue,
                taskEvent.Agent?.Name ?? "Coordinator",
                taskEvent.Timestamp))
            .ToList();

        var decisions = task.Decisions
            .OrderBy(decision => decision.CreatedAt)
            .Select(decision => new TaskContextDecision(
                decision.Id,
                decision.Title,
                decision.Content,
                decision.Status,
                decision.Agent?.Name ?? string.Empty,
                decision.CreatedAt))
            .ToList();

        var taskCore = new TaskContextTask(
            task.Id,
            task.ProjectId,
            task.EpicId,
            task.ParentTaskId,
            task.AssignedAgentId,
            task.Title,
            task.Description,
            task.Status,
            task.BlockedReason,
            new Dictionary<string, string>(task.Metadata),
            task.CreatedAt,
            task.UpdatedAt);

        var project = new TaskContextProject(
            task.Project?.Id ?? task.ProjectId,
            task.Project?.Name ?? string.Empty);

        var epic = task.Epic is null
            ? null
            : new TaskContextEpic(
                task.Epic.Id,
                task.Epic.Title,
                task.Epic.Description,
                task.Epic.Status);

        var parentTask = task.ParentTask is null
            ? null
            : new TaskContextParentTask(
                task.ParentTask.Id,
                task.ParentTask.Title,
                task.ParentTask.Status);

        return new TaskContext(
            taskCore,
            project,
            epic,
            parentTask,
            subtasks,
            new TaskContextDependencies(blockedBy, blocking),
            notes,
            events,
            decisions);
    }
}
