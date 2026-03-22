using Hiveboard.Api.Auth;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
            .WithDescription("Auth: Any agent API key. Lists epics when the project belongs to the caller's organization.")
            .Produces<List<EpicResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/projects/{projectId:guid}/epics", CreateEpic)
            .RequireAuthorization("OrchestratorOnly")
            .WithName("CreateEpic")
            .WithSummary("Create an epic")
            .WithDescription("Auth: Orchestrator agent API key. Creates an epic under a project in the caller's organization.")
            .Produces<EpicResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/epics/{id:guid}", GetEpicById)
            .WithName("GetEpicById")
            .WithSummary("Get epic details")
            .WithDescription("Auth: Any agent API key. Returns an epic together with its tasks when it belongs to the caller's organization.")
            .Produces<EpicResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListProjectEpics(
        Guid projectId,
        AgentContext agentContext,
        HiveboardDbContext db)
    {
        var scopeError = ValidateOrganizationScope(agentContext);
        if (scopeError is not null)
            return scopeError;

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId);

        if (project is null)
            return Results.NotFound(new { error = "Project not found" });

        if (project.OrganizationId != agentContext.OrganizationId)
            return Forbidden("Project belongs to a different organization");

        var epics = await db.Epics
            .AsNoTracking()
            .Where(epic => epic.ProjectId == projectId)
            .ToListAsync();

        return Results.Ok(epics
            .OrderBy(epic => epic.CreatedAt)
            .Select(epic => ToEpicResponse(epic, includeTasks: false)));
    }

    private static async Task<IResult> CreateEpic(
        Guid projectId,
        CreateEpicRequest? request,
        AgentContext agentContext,
        HiveboardDbContext db)
    {
        var scopeError = ValidateOrganizationScope(agentContext);
        if (scopeError is not null)
            return scopeError;

        if (agentContext.AgentType != AgentType.Orchestrator || agentContext.AgentId == Guid.Empty)
            return Forbidden("Only orchestrator agents can create epics");

        if (request is null || string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(new { error = "Title is required" });

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId);

        if (project is null)
            return Results.NotFound(new { error = "Project not found" });

        if (project.OrganizationId != agentContext.OrganizationId)
            return Forbidden("Project belongs to a different organization");

        var epic = new Epic
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Status = EpicStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Epics.Add(epic);
        await db.SaveChangesAsync();

        return Results.CreatedAtRoute(
            "GetEpicById",
            new { id = epic.Id },
            ToEpicResponse(epic, includeTasks: false));
    }

    private static async Task<IResult> GetEpicById(
        Guid id,
        AgentContext agentContext,
        HiveboardDbContext db)
    {
        var scopeError = ValidateOrganizationScope(agentContext);
        if (scopeError is not null)
            return scopeError;

        var epic = await db.Epics
            .AsNoTracking()
            .Include(candidate => candidate.Project)
            .Include(candidate => candidate.Tasks)
            .FirstOrDefaultAsync(candidate => candidate.Id == id);

        if (epic is null)
            return Results.NotFound(new { error = "Epic not found" });

        if (epic.Project?.OrganizationId != agentContext.OrganizationId)
            return Forbidden("Epic belongs to a different organization");

        return Results.Ok(ToEpicResponse(epic, includeTasks: true));
    }

    private static EpicResponse ToEpicResponse(Epic epic, bool includeTasks) =>
        new(
            epic.Id,
            epic.ProjectId,
            epic.Title,
            string.IsNullOrWhiteSpace(epic.Description) ? null : epic.Description,
            epic.Status.ToString().ToLowerInvariant(),
            epic.CreatedAt,
            includeTasks
                ? epic.Tasks
                    .OrderBy(task => task.CreatedAt)
                    .Select(task => new EpicTaskSummaryResponse(
                        task.Id,
                        task.Title,
                        task.Description,
                        task.Status.ToString().ToLowerInvariant(),
                        task.AssignedAgentId,
                        task.CreatedAt,
                        task.UpdatedAt))
                    .ToList()
                : null);

    private static IResult? ValidateOrganizationScope(AgentContext agentContext)
    {
        if (agentContext.IsAdmin || agentContext.OrganizationId == Guid.Empty)
            return Forbidden("Organization-scoped endpoints require an agent API key");

        return null;
    }

    private static IResult Forbidden(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
}
