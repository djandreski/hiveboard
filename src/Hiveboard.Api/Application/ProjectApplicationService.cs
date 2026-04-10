using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Core.Services;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Api.Application;

public sealed class ProjectApplicationService
{
    private readonly HiveboardDbContext _db;
    private readonly AgentContext _agentContext;
    private readonly IAgentAccessGuard _accessGuard;

    public ProjectApplicationService(
        HiveboardDbContext db,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard)
    {
        _db = db;
        _agentContext = agentContext;
        _accessGuard = accessGuard;
    }

    public async Task<IResult> ListProjectsAsync()
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var projects = await _db.Projects
            .AsNoTracking()
            .Where(project => project.OrganizationId == _agentContext.OrganizationId)
            .ToListAsync();

        return Results.Ok(projects
            .OrderBy(project => project.CreatedAt)
            .Select(ToProjectResponse));
    }

    public async Task<IResult> GetProjectByIdAsync(Guid projectId)
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

        return Results.Ok(ToProjectResponse(project));
    }

    public async Task<IResult> CreateProjectAsync(CreateProjectRequest? request)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var coordinatorOrOrchestratorError = _accessGuard.ValidateCoordinatorOrOrchestratorScope(
            _agentContext,
            "Only coordinators or orchestrator agents can create projects");
        if (coordinatorOrOrchestratorError is not null)
            return coordinatorOrOrchestratorError;

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required" });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = _agentContext.OrganizationId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Projects.Add(project);

        if (_agentContext.IsOrchestrator)
            _db.Entry(project).Property<Guid?>("OrchestratorAgentId").CurrentValue = _agentContext.AgentId;

        await _db.SaveChangesAsync();

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

    private static IResult Forbidden(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
}
