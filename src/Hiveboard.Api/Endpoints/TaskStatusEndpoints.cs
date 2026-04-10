using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class TaskStatusEndpoints
{
    public static void MapTaskStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Tasks")
            .RequireAuthorization();

        group.MapPatch("/tasks/{id:guid}/status", UpdateTaskStatus)
            .WithName("UpdateTaskStatus")
            .WithSummary("Transition task status")
            .WithDescription("Auth: any authenticated key. Workers can transition their assigned tasks, coordinators can perform coordinator-side transitions, and the configured project orchestrator can perform coordinator-side transitions when attached to the project.")
            .Produces<TaskResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    private static Task<IResult> UpdateTaskStatus(
        Guid id,
        UpdateTaskStatusRequest? request,
        TaskApplicationService applicationService)
        => applicationService.UpdateTaskStatusAsync(id, request);
}
