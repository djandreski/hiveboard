using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class DependencyEndpoints
{
    public static void MapDependencyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Dependencies")
            .RequireAuthorization();

        group.MapPost("/tasks/{id:guid}/dependencies", AddDependency)
            .RequireAuthorization("CoordinatorOrOrchestratorOnly")
            .WithName("AddTaskDependency")
            .WithSummary("Add a task dependency")
            .WithDescription("Auth: coordinator/admin key or the configured project orchestrator API key. Creates a blocking dependency for the selected task.")
            .Produces<TaskDependencyResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/tasks/{id:guid}/dependencies/{depId:guid}", RemoveDependency)
            .RequireAuthorization("CoordinatorOrOrchestratorOnly")
            .WithName("RemoveTaskDependency")
            .WithSummary("Remove a task dependency")
            .WithDescription("Auth: coordinator/admin key or the configured project orchestrator API key. Removes the selected dependency from the task.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/projects/{projectId:guid}/dependencies/graph", GetDependencyGraph)
            .WithName("GetDependencyGraph")
            .WithSummary("Get the project dependency graph")
            .WithDescription("Auth: coordinator/admin key or any agent API key. Returns graph nodes and edges for dependency visualization.")
            .Produces<DependencyGraphResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static Task<IResult> AddDependency(
        Guid id,
        CreateDependencyRequest? request,
        DependencyApplicationService applicationService,
        CancellationToken cancellationToken) =>
        applicationService.AddDependencyAsync(id, request, cancellationToken);

    private static Task<IResult> RemoveDependency(
        Guid id,
        Guid depId,
        DependencyApplicationService applicationService,
        CancellationToken cancellationToken) =>
        applicationService.RemoveDependencyAsync(id, depId, cancellationToken);

    private static Task<IResult> GetDependencyGraph(
        Guid projectId,
        DependencyApplicationService applicationService,
        CancellationToken cancellationToken) =>
        applicationService.GetDependencyGraphAsync(projectId, cancellationToken);
}
