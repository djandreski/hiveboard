using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;

namespace Hiveboard.Core.Services;

public interface IDependencyRepository
{
    Task<AgentTask?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentTask>> GetProjectTasksAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskDependency>> GetProjectDependenciesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> DependencyExistsAsync(Guid taskId, Guid dependsOnTaskId, CancellationToken cancellationToken = default);
    void AddDependency(TaskDependency dependency);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public enum DependencyAddFailureKind
{
    None,
    TaskNotFound,
    DependsOnTaskNotFound,
    SelfDependency,
    CrossProject,
    Duplicate,
    CircularDependency
}

public sealed record DependencyCycleNode(Guid TaskId, string Title);

public sealed record DependencyAddResult(
    bool IsSuccess,
    DependencyAddFailureKind FailureKind,
    string? ErrorMessage,
    TaskDependency? Dependency,
    IReadOnlyList<DependencyCycleNode> CyclePath)
{
    public static DependencyAddResult Success(TaskDependency dependency) =>
        new(true, DependencyAddFailureKind.None, null, dependency, Array.Empty<DependencyCycleNode>());

    public static DependencyAddResult Failure(
        DependencyAddFailureKind failureKind,
        string errorMessage,
        IReadOnlyList<DependencyCycleNode>? cyclePath = null) =>
        new(false, failureKind, errorMessage, null, cyclePath ?? Array.Empty<DependencyCycleNode>());
}

public sealed record DependencyGraphNode(
    Guid TaskId,
    string Title,
    string Status,
    Guid? AssignedAgentId);

public sealed record DependencyGraphEdge(
    Guid From,
    Guid To,
    string Type,
    Guid DepId);

public sealed record DependencyGraph(
    IReadOnlyList<DependencyGraphNode> Nodes,
    IReadOnlyList<DependencyGraphEdge> Edges);

public sealed class DependencyService
{
    private readonly IDependencyRepository _repository;

    public DependencyService(IDependencyRepository repository)
    {
        _repository = repository;
    }

    public async Task<DependencyAddResult> AddDependencyAsync(
        Guid taskId,
        Guid dependsOnTaskId,
        CancellationToken cancellationToken = default)
    {
        var task = await _repository.GetTaskAsync(taskId, cancellationToken);
        if (task is null)
        {
            return DependencyAddResult.Failure(
                DependencyAddFailureKind.TaskNotFound,
                "Task not found.");
        }

        if (taskId == dependsOnTaskId)
        {
            return DependencyAddResult.Failure(
                DependencyAddFailureKind.SelfDependency,
                "A task cannot depend on itself.");
        }

        var dependsOnTask = await _repository.GetTaskAsync(dependsOnTaskId, cancellationToken);
        if (dependsOnTask is null)
        {
            return DependencyAddResult.Failure(
                DependencyAddFailureKind.DependsOnTaskNotFound,
                "Dependency task not found.");
        }

        if (task.ProjectId != dependsOnTask.ProjectId)
        {
            return DependencyAddResult.Failure(
                DependencyAddFailureKind.CrossProject,
                "Dependencies can only be created between tasks in the same project.");
        }

        if (await _repository.DependencyExistsAsync(taskId, dependsOnTaskId, cancellationToken))
        {
            return DependencyAddResult.Failure(
                DependencyAddFailureKind.Duplicate,
                "Dependency already exists.");
        }

        var projectTasks = await _repository.GetProjectTasksAsync(task.ProjectId, cancellationToken);
        var projectDependencies = await _repository.GetProjectDependenciesAsync(task.ProjectId, cancellationToken);

        var existingPath = FindDependencyPath(projectDependencies, dependsOnTaskId, taskId);
        if (existingPath is not null)
        {
            var taskTitles = projectTasks.ToDictionary(candidate => candidate.Id, candidate => candidate.Title);
            var cycleTaskIds = new List<Guid> { taskId };
            cycleTaskIds.AddRange(existingPath);

            var cyclePath = cycleTaskIds
                .Select(candidate => new DependencyCycleNode(
                    candidate,
                    taskTitles.TryGetValue(candidate, out var title)
                        ? title
                        : $"Task {candidate}"))
                .ToList();

            return DependencyAddResult.Failure(
                DependencyAddFailureKind.CircularDependency,
                $"Circular dependency detected: {string.Join(" -> ", cyclePath.Select(candidate => candidate.Title))}.",
                cyclePath);
        }

        var dependency = new TaskDependency
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            DependsOnTaskId = dependsOnTaskId,
            Type = DependencyType.Blocks
        };

        _repository.AddDependency(dependency);
        await _repository.SaveChangesAsync(cancellationToken);

        return DependencyAddResult.Success(dependency);
    }

    public async Task<DependencyGraph> GetDependencyGraphAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await _repository.GetProjectTasksAsync(projectId, cancellationToken);
        var dependencies = await _repository.GetProjectDependenciesAsync(projectId, cancellationToken);

        var nodes = tasks
            .OrderBy(candidate => candidate.CreatedAt)
            .Select(candidate => new DependencyGraphNode(
                candidate.Id,
                candidate.Title,
                candidate.Status.ToString(),
                candidate.AssignedAgentId))
            .ToList();

        var edges = dependencies
            .OrderBy(candidate => candidate.TaskId)
            .ThenBy(candidate => candidate.DependsOnTaskId)
            .Select(candidate => new DependencyGraphEdge(
                candidate.TaskId,
                candidate.DependsOnTaskId,
                candidate.Type.ToString().ToLowerInvariant(),
                candidate.Id))
            .ToList();

        return new DependencyGraph(nodes, edges);
    }

    private static IReadOnlyList<Guid>? FindDependencyPath(
        IReadOnlyList<TaskDependency> dependencies,
        Guid startTaskId,
        Guid targetTaskId)
    {
        var adjacency = dependencies
            .GroupBy(candidate => candidate.TaskId)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => grouping
                    .Select(candidate => candidate.DependsOnTaskId)
                    .Distinct()
                    .ToList());

        var visited = new HashSet<Guid> { startTaskId };
        var queue = new Queue<Guid>();
        var previous = new Dictionary<Guid, Guid>();

        queue.Enqueue(startTaskId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == targetTaskId)
                return ReconstructPath(startTaskId, targetTaskId, previous);

            if (!adjacency.TryGetValue(current, out var nextTaskIds))
                continue;

            foreach (var nextTaskId in nextTaskIds)
            {
                if (!visited.Add(nextTaskId))
                    continue;

                previous[nextTaskId] = current;
                queue.Enqueue(nextTaskId);
            }
        }

        return null;
    }

    private static IReadOnlyList<Guid> ReconstructPath(
        Guid startTaskId,
        Guid targetTaskId,
        IReadOnlyDictionary<Guid, Guid> previous)
    {
        var path = new List<Guid> { targetTaskId };
        var current = targetTaskId;

        while (current != startTaskId)
        {
            current = previous[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
