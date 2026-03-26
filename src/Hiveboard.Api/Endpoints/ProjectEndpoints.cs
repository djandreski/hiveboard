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
            .WithDescription("Auth: Any agent API key. Lists projects for the caller's organization.")
            .Produces<List<ProjectResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetProjectById)
            .WithName("GetProjectById")
            .WithSummary("Get project details")
            .WithDescription("Auth: Any agent API key. Returns a project when it belongs to the caller's organization.")
            .Produces<ProjectResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateProject)
            .RequireAuthorization("OrchestratorOnly")
            .WithName("CreateProject")
            .WithSummary("Create a project")
            .WithDescription("Auth: Orchestrator agent API key. Creates a project in the caller's organization and assigns the caller as orchestrator.")
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
