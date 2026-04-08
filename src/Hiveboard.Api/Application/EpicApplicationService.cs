using Hiveboard.Api.Auth;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Api.Application;

public sealed class EpicApplicationService
{
    private readonly HiveboardDbContext _db;
    private readonly AgentContext _agentContext;
    private readonly IAgentAccessGuard _accessGuard;

    public EpicApplicationService(
        HiveboardDbContext db,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard)
    {
        _db = db;
        _agentContext = agentContext;
        _accessGuard = accessGuard;
    }

    public async Task<IResult> ListProjectEpicsAsync(Guid projectId)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId);

        if (project is null)
            return Results.NotFound(new { error = "Project not found" });

        if (project.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Project belongs to a different organization");

        var epics = await _db.Epics
            .AsNoTracking()
            .Where(epic => epic.ProjectId == projectId)
            .ToListAsync();

        return Results.Ok(epics
            .OrderBy(epic => epic.CreatedAt)
            .Select(epic => ToEpicResponse(epic, includeTasks: false)));
    }

    public async Task<IResult> CreateEpicAsync(Guid projectId, CreateEpicRequest? request)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var coordinatorOrOrchestratorError = _accessGuard.ValidateCoordinatorOrOrchestratorScope(
            _agentContext,
            "Only coordinators or orchestrator agents can create epics");
        if (coordinatorOrOrchestratorError is not null)
            return coordinatorOrOrchestratorError;

        if (request is null || string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(new { error = "Title is required" });

        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId);

        if (project is null)
            return Results.NotFound(new { error = "Project not found" });

        if (project.OrganizationId != _agentContext.OrganizationId)
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

        _db.Epics.Add(epic);
        await _db.SaveChangesAsync();

        return Results.CreatedAtRoute(
            "GetEpicById",
            new { id = epic.Id },
            ToEpicResponse(epic, includeTasks: false));
    }

    public async Task<IResult> GetEpicByIdAsync(Guid epicId)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var epic = await _db.Epics
            .AsNoTracking()
            .Include(candidate => candidate.Project)
            .Include(candidate => candidate.Tasks)
            .FirstOrDefaultAsync(candidate => candidate.Id == epicId);

        if (epic is null)
            return Results.NotFound(new { error = "Epic not found" });

        if (epic.Project?.OrganizationId != _agentContext.OrganizationId)
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

    private static IResult Forbidden(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
}
