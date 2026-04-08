using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class EpicEndpoints
{
    public static void MapEpicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Epics")
            .RequireAuthorization();

        group.MapGet("/projects/{projectId:guid}/epics", ListProjectEpics)
            .WithName("ListProjectEpics")
            .WithSummary("List epics for a project")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Lists epics when the project belongs to the caller's organization scope.")
            .Produces<List<EpicResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/projects/{projectId:guid}/epics", CreateEpic)
            .RequireAuthorization("CoordinatorOrOrchestratorOnly")
            .WithName("CreateEpic")
            .WithSummary("Create an epic")
            .WithDescription("Auth: Coordinator/admin key or orchestrator agent API key. Creates an epic under a project in the caller's organization scope.")
            .Produces<EpicResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/epics/{id:guid}", GetEpicById)
            .WithName("GetEpicById")
            .WithSummary("Get epic details")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Returns an epic together with its tasks when it belongs to the caller's organization scope.")
            .Produces<EpicResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static Task<IResult> ListProjectEpics(
        Guid projectId,
        EpicApplicationService applicationService)
        => applicationService.ListProjectEpicsAsync(projectId);

    private static Task<IResult> CreateEpic(
        Guid projectId,
        CreateEpicRequest? request,
        EpicApplicationService applicationService)
        => applicationService.CreateEpicAsync(projectId, request);

    private static Task<IResult> GetEpicById(
        Guid id,
        EpicApplicationService applicationService)
        => applicationService.GetEpicByIdAsync(id);
}
