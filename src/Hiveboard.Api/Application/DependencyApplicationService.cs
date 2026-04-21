using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Services;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Api.Application;

public sealed class DependencyApplicationService
{
    private readonly HiveboardDbContext _db;
    private readonly AgentContext _agentContext;
    private readonly IAgentAccessGuard _accessGuard;
    private readonly DependencyService _dependencyService;

    public DependencyApplicationService(
        HiveboardDbContext db,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard,
        DependencyService dependencyService)
    {
        _db = db;
        _agentContext = agentContext;
        _accessGuard = accessGuard;
        _dependencyService = dependencyService;
    }

    public async Task<IResult> AddDependencyAsync(
        Guid taskId,
        CreateDependencyRequest? request,
        CancellationToken cancellationToken)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var coordinatorOrOrchestratorError = _accessGuard.ValidateCoordinatorOrOrchestratorScope(
            _agentContext,
            "Only coordinators or orchestrator agents can add dependencies");
        if (coordinatorOrOrchestratorError is not null)
            return coordinatorOrOrchestratorError;

        if (request is null || request.DependsOnTaskId == Guid.Empty)
            return Results.BadRequest(new { error = "DependsOnTaskId is required" });

        var task = await _db.AgentTasks
            .Include(candidate => candidate.Project)
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken);

        if (task is null)
            return Results.NotFound(new { error = "Task not found" });

        if (task.Project?.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Task belongs to a different organization");

        var projectWriteAccessError = _accessGuard.ValidateCoordinatorOrConfiguredProjectOrchestratorScope(
            _agentContext,
            task.Project?.OrchestratorAgentId,
            "Only the coordinator or the project's configured orchestrator can add dependencies");
        if (projectWriteAccessError is not null)
            return projectWriteAccessError;

        task.UpdatedAt = DateTimeOffset.UtcNow;
        var addResult = await _dependencyService.AddDependencyAsync(taskId, request.DependsOnTaskId, cancellationToken);
        if (!addResult.IsSuccess)
            return CreateAddDependencyFailureResult(addResult);

        return Results.Created(
            $"/api/v1/tasks/{taskId}/dependencies/{addResult.Dependency!.Id}",
            ToDependencyResponse(addResult.Dependency));
    }

    public async Task<IResult> RemoveDependencyAsync(
        Guid taskId,
        Guid dependencyId,
        CancellationToken cancellationToken)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var coordinatorOrOrchestratorError = _accessGuard.ValidateCoordinatorOrOrchestratorScope(
            _agentContext,
            "Only coordinators or orchestrator agents can remove dependencies");
        if (coordinatorOrOrchestratorError is not null)
            return coordinatorOrOrchestratorError;

        var dependency = await _db.TaskDependencies
            .Include(candidate => candidate.Task)
                .ThenInclude(task => task!.Project)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == dependencyId && candidate.TaskId == taskId,
                cancellationToken);

        if (dependency is null)
            return Results.NotFound(new { error = "Dependency not found" });

        if (dependency.Task?.Project?.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Dependency belongs to a different organization");

        var projectWriteAccessError = _accessGuard.ValidateCoordinatorOrConfiguredProjectOrchestratorScope(
            _agentContext,
            dependency.Task.Project?.OrchestratorAgentId,
            "Only the coordinator or the project's configured orchestrator can remove dependencies");
        if (projectWriteAccessError is not null)
            return projectWriteAccessError;

        dependency.Task.UpdatedAt = DateTimeOffset.UtcNow;
        _db.TaskDependencies.Remove(dependency);
        await _db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    public async Task<IResult> GetDependencyGraphAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
            return Results.NotFound(new { error = "Project not found" });

        if (project.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Project belongs to a different organization");

        var graph = await _dependencyService.GetDependencyGraphAsync(projectId, cancellationToken);

        return Results.Ok(new DependencyGraphResponse(
            graph.Nodes
                .Select(candidate => new DependencyGraphNodeResponse(
                    candidate.TaskId,
                    candidate.Title,
                    candidate.Status,
                    candidate.AssignedAgentId))
                .ToList(),
            graph.Edges
                .Select(candidate => new DependencyGraphEdgeResponse(
                    candidate.From,
                    candidate.To,
                    candidate.Type))
                .ToList()));
    }

    private static TaskDependencyResponse ToDependencyResponse(TaskDependency dependency) =>
        new(
            dependency.Id,
            dependency.TaskId,
            dependency.DependsOnTaskId,
            dependency.Type.ToString().ToLowerInvariant());

    private static IResult CreateAddDependencyFailureResult(DependencyAddResult result)
    {
        return result.FailureKind switch
        {
            DependencyAddFailureKind.TaskNotFound or DependencyAddFailureKind.DependsOnTaskNotFound =>
                Results.NotFound(new { error = result.ErrorMessage }),
            DependencyAddFailureKind.Duplicate =>
                Results.Conflict(new { error = result.ErrorMessage }),
            DependencyAddFailureKind.CircularDependency =>
                Results.BadRequest(new
                {
                    error = result.ErrorMessage,
                    cyclePath = result.CyclePath.Select(candidate => new
                    {
                        candidate.TaskId,
                        candidate.Title
                    })
                }),
            _ => Results.BadRequest(new { error = result.ErrorMessage })
        };
    }

    private static IResult Forbidden(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
}

internal sealed class EfCoreDependencyRepository : IDependencyRepository
{
    private readonly HiveboardDbContext _db;

    public EfCoreDependencyRepository(HiveboardDbContext db)
    {
        _db = db;
    }

    public Task<AgentTask?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        _db.AgentTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken);

    public async Task<IReadOnlyList<AgentTask>> GetProjectTasksAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        await _db.AgentTasks
            .AsNoTracking()
            .Where(candidate => candidate.ProjectId == projectId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskDependency>> GetProjectDependenciesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        await _db.TaskDependencies
            .AsNoTracking()
            .Where(candidate => candidate.Task != null && candidate.Task.ProjectId == projectId)
            .ToListAsync(cancellationToken);

    public Task<bool> DependencyExistsAsync(
        Guid taskId,
        Guid dependsOnTaskId,
        CancellationToken cancellationToken = default) =>
        _db.TaskDependencies
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.TaskId == taskId && candidate.DependsOnTaskId == dependsOnTaskId,
                cancellationToken);

    public void AddDependency(TaskDependency dependency) => _db.TaskDependencies.Add(dependency);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
