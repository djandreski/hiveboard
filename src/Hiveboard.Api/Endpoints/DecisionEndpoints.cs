using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class DecisionEndpoints
{
    public static void MapDecisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Decision Records")
            .RequireAuthorization();

        group.MapPost("/projects/{projectId:guid}/decisions", CreateDecision)
            .WithName("CreateDecision")
            .WithSummary("Create a decision record")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Creates a project-level decision record with optional task linkage and free-form markdown content.")
            .Produces<DecisionResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/projects/{projectId:guid}/decisions", ListProjectDecisions)
            .WithName("ListProjectDecisions")
            .WithSummary("List decision records for a project")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Returns project decision records in reverse chronological order and supports optional status and task filters.")
            .Produces<List<DecisionResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/decisions/{id:guid}", GetDecisionById)
            .WithName("GetDecisionById")
            .WithSummary("Get a decision record")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Returns a single decision record when it belongs to the caller's organization scope.")
            .Produces<DecisionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static Task<IResult> CreateDecision(
        Guid projectId,
        CreateDecisionRequest? request,
        NotesAndDecisionsApplicationService applicationService) =>
        applicationService.CreateDecisionAsync(projectId, request);

    private static Task<IResult> ListProjectDecisions(
        Guid projectId,
        string? status,
        Guid? taskId,
        NotesAndDecisionsApplicationService applicationService) =>
        applicationService.ListProjectDecisionsAsync(projectId, status, taskId);

    private static Task<IResult> GetDecisionById(
        Guid id,
        NotesAndDecisionsApplicationService applicationService) =>
        applicationService.GetDecisionByIdAsync(id);
}
