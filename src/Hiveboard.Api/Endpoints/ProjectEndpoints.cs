using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/projects")
            .WithTags("Projects")
            .RequireAuthorization();

        group.MapGet("/", ListProjects)
            .WithName("ListProjects")
            .WithSummary("List projects")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Lists projects for the caller's organization scope.")
            .Produces<List<ProjectResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetProjectById)
            .WithName("GetProjectById")
            .WithSummary("Get project details")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Returns a project when it belongs to the caller's organization scope.")
            .Produces<ProjectResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateProject)
            .RequireAuthorization("CoordinatorOrOrchestratorOnly")
            .WithName("CreateProject")
            .WithSummary("Create a project")
            .WithDescription("Auth: Coordinator/admin key or orchestrator agent API key. Creates a project in the caller's organization. Coordinator-created projects start without an orchestrator; orchestrator-created projects attach the caller as the optional project orchestrator.")
            .Produces<ProjectResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static Task<IResult> ListProjects(ProjectApplicationService applicationService)
        => applicationService.ListProjectsAsync();

    private static Task<IResult> GetProjectById(
        Guid id,
        ProjectApplicationService applicationService)
        => applicationService.GetProjectByIdAsync(id);

    private static Task<IResult> CreateProject(
        CreateProjectRequest? request,
        ProjectApplicationService applicationService)
        => applicationService.CreateProjectAsync(request);
}
