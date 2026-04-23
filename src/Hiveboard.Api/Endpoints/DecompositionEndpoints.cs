using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class DecompositionEndpoints
{
    public static void MapTaskDecompositionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Tasks")
            .RequireAuthorization();

        group.MapPost("/tasks/{id:guid}/subtasks", DecomposeTask)
            .WithName("DecomposeTask")
            .WithSummary("Decompose a task into subtasks")
            .WithDescription("Auth: any authenticated key. The assigned worker, the coordinator/admin key, or the configured project orchestrator can create backlog subtasks for an assigned or in-progress parent task.")
            .Produces<List<TaskResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    private static Task<IResult> DecomposeTask(
        Guid id,
        DecomposeTaskRequest? request,
        TaskApplicationService applicationService)
        => applicationService.DecomposeTaskAsync(id, request);
}
