using Hiveboard.Api.Auth;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

    private static async Task<IResult> ListProjects(
        AgentContext agentContext,
        HiveboardDbContext db)
    {
        var scopeError = ValidateOrganizationScope(agentContext);
        if (scopeError is not null)
            return scopeError;

        var projects = await db.Projects
            .AsNoTracking()
            .Where(project => project.OrganizationId == agentContext.OrganizationId)
            .ToListAsync();

        return Results.Ok(projects
            .OrderBy(project => project.CreatedAt)
            .Select(ToProjectResponse));
    }

    private static async Task<IResult> GetProjectById(
        Guid id,
        AgentContext agentContext,
        HiveboardDbContext db)
    {
        var scopeError = ValidateOrganizationScope(agentContext);
        if (scopeError is not null)
            return scopeError;

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id);

        if (project is null)
            return Results.NotFound(new { error = "Project not found" });

        if (project.OrganizationId != agentContext.OrganizationId)
            return Forbidden("Project belongs to a different organization");

        return Results.Ok(ToProjectResponse(project));
    }

    private static async Task<IResult> CreateProject(
        CreateProjectRequest? request,
        AgentContext agentContext,
        HiveboardDbContext db)
    {
        var scopeError = ValidateOrganizationScope(agentContext);
        if (scopeError is not null)
            return scopeError;

        if (agentContext.AgentType != AgentType.Orchestrator || agentContext.AgentId == Guid.Empty)
            return Forbidden("Only orchestrator agents can create projects");

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required" });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = agentContext.OrganizationId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Projects.Add(project);
        db.Entry(project).Property<Guid?>("OrchestratorAgentId").CurrentValue = agentContext.AgentId;

        await db.SaveChangesAsync();

        return Results.CreatedAtRoute(
            "GetProjectById",
            new { id = project.Id },
            ToProjectResponse(project));
    }

    private static ProjectResponse ToProjectResponse(Project project) =>
        new(
            project.Id,
            project.Name,
            string.IsNullOrWhiteSpace(project.Description) ? null : project.Description,
            project.Status.ToString().ToLowerInvariant(),
            project.CreatedAt);

    private static IResult? ValidateOrganizationScope(AgentContext agentContext)
    {
        if (agentContext.IsAdmin || agentContext.OrganizationId == Guid.Empty)
            return Forbidden("Organization-scoped endpoints require an agent API key");

        return null;
    }

    private static IResult Forbidden(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
}
