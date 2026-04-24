using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Core.Services;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Api.Application;

public sealed class NotesAndDecisionsApplicationService
{
    private const string CoordinatorAuditAgentName = "Coordinator";

    private readonly HiveboardDbContext _db;
    private readonly AgentContext _agentContext;
    private readonly IAgentAccessGuard _accessGuard;

    public NotesAndDecisionsApplicationService(
        HiveboardDbContext db,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard)
    {
        _db = db;
        _agentContext = agentContext;
        _accessGuard = accessGuard;
    }

    public async Task<IResult> CreateTaskNoteAsync(Guid taskId, CreateNoteRequest? request)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        if (request is null || string.IsNullOrWhiteSpace(request.Content))
            return Results.BadRequest(new { error = "Content is required" });

        if (!TryParseApiEnum<NoteType>(request.NoteType, out var noteType))
        {
            return Results.BadRequest(new
            {
                error = "Invalid noteType. Must be one of: Context, Progress, ReviewRequest, Blocker, Resolution"
            });
        }

        var task = await _db.AgentTasks
            .AsNoTracking()
            .Include(candidate => candidate.Project)
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId);

        if (task is null)
            return Results.NotFound(new { error = "Task not found" });

        if (task.Project?.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Task belongs to a different organization");

        var now = DateTimeOffset.UtcNow;
        var actorAgentId = await GetActorAgentIdAsync(now);
        var note = new TaskNote
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AgentId = actorAgentId,
            Content = request.Content.Trim(),
            NoteType = noteType,
            CreatedAt = now
        };

        _db.TaskNotes.Add(note);
        _db.TaskEvents.Add(new TaskEvent
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AgentId = actorAgentId,
            EventType = "note_added",
            OldValue = null,
            NewValue = ToApiValue(noteType),
            Timestamp = now
        });

        await _db.SaveChangesAsync();

        var response = new NoteResponse(
            note.Id,
            note.TaskId,
            note.AgentId,
            GetCurrentActorName(),
            ToApiValue(GetCurrentActorType()),
            note.Content,
            ToApiValue(note.NoteType),
            note.CreatedAt);

        return Results.Created($"/api/v1/tasks/{taskId}/notes/{note.Id}", response);
    }

    public async Task<IResult> ListTaskNotesAsync(Guid taskId)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var task = await _db.AgentTasks
            .AsNoTracking()
            .Include(candidate => candidate.Project)
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId);

        if (task is null)
            return Results.NotFound(new { error = "Task not found" });

        if (task.Project?.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Task belongs to a different organization");

        var notes = await _db.TaskNotes
            .AsNoTracking()
            .Where(note => note.TaskId == taskId)
            .Include(note => note.Agent)
            .ToListAsync();

        return Results.Ok(notes
            .OrderBy(note => note.CreatedAt)
            .Select(ToNoteResponse)
            .ToList());
    }

    public async Task<IResult> CreateDecisionAsync(Guid projectId, CreateDecisionRequest? request)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        if (request is null || string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(new { error = "Title is required" });

        if (string.IsNullOrWhiteSpace(request.Content))
            return Results.BadRequest(new { error = "Content is required" });

        if (!TryParseApiEnum<DecisionStatus>(request.Status, out var decisionStatus))
        {
            return Results.BadRequest(new
            {
                error = "Invalid status. Must be one of: Proposed, Accepted, Superseded"
            });
        }

        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId);

        if (project is null)
            return Results.NotFound(new { error = "Project not found" });

        if (project.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Project belongs to a different organization");

        if (request.TaskId.HasValue)
        {
            var relatedTask = await _db.AgentTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == request.TaskId.Value);

            if (relatedTask is null)
                return Results.NotFound(new { error = "Task not found" });

            if (relatedTask.ProjectId != projectId)
                return Results.BadRequest(new { error = "Task does not belong to the selected project" });
        }

        var now = DateTimeOffset.UtcNow;
        var actorAgentId = await GetActorAgentIdAsync(now);
        var decision = new DecisionRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TaskId = request.TaskId,
            AgentId = actorAgentId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Status = decisionStatus,
            CreatedAt = now
        };

        _db.DecisionRecords.Add(decision);
        await _db.SaveChangesAsync();

        var response = new DecisionResponse(
            decision.Id,
            decision.ProjectId,
            decision.TaskId,
            decision.AgentId,
            GetCurrentActorName(),
            ToApiValue(GetCurrentActorType()),
            decision.Title,
            decision.Content,
            ToApiValue(decision.Status),
            decision.CreatedAt);

        return Results.CreatedAtRoute("GetDecisionById", new { id = decision.Id }, response);
    }

    public async Task<IResult> ListProjectDecisionsAsync(Guid projectId, string? status, Guid? taskId)
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

        var query = _db.DecisionRecords
            .AsNoTracking()
            .Where(decision => decision.ProjectId == projectId)
            .Include(decision => decision.Agent)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseApiEnum<DecisionStatus>(status, out var parsedStatus))
            {
                return Results.BadRequest(new
                {
                    error = "Invalid status filter. Must be one of: Proposed, Accepted, Superseded"
                });
            }

            query = query.Where(decision => decision.Status == parsedStatus);
        }

        if (taskId.HasValue)
            query = query.Where(decision => decision.TaskId == taskId.Value);

        var decisions = await query.ToListAsync();

        return Results.Ok(decisions
            .OrderByDescending(decision => decision.CreatedAt)
            .Select(ToDecisionResponse)
            .ToList());
    }

    public async Task<IResult> GetDecisionByIdAsync(Guid decisionId)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var decision = await _db.DecisionRecords
            .AsNoTracking()
            .Include(candidate => candidate.Project)
            .Include(candidate => candidate.Agent)
            .FirstOrDefaultAsync(candidate => candidate.Id == decisionId);

        if (decision is null)
            return Results.NotFound(new { error = "Decision record not found" });

        if (decision.Project?.OrganizationId != _agentContext.OrganizationId)
            return Forbidden("Decision record belongs to a different organization");

        return Results.Ok(ToDecisionResponse(decision));
    }

    private static NoteResponse ToNoteResponse(TaskNote note) =>
        new(
            note.Id,
            note.TaskId,
            note.AgentId,
            note.Agent?.Name ?? string.Empty,
            ToApiValue(note.Agent?.Type ?? AgentType.Orchestrator),
            note.Content,
            ToApiValue(note.NoteType),
            note.CreatedAt);

    private static DecisionResponse ToDecisionResponse(DecisionRecord decision) =>
        new(
            decision.Id,
            decision.ProjectId,
            decision.TaskId,
            decision.AgentId,
            decision.Agent?.Name ?? string.Empty,
            ToApiValue(decision.Agent?.Type ?? AgentType.Orchestrator),
            decision.Title,
            decision.Content,
            ToApiValue(decision.Status),
            decision.CreatedAt);

    private async Task<Guid> GetActorAgentIdAsync(DateTimeOffset now)
    {
        if (_agentContext.IsCoordinator)
            return await GetOrCreateCoordinatorAuditAgentIdAsync(now);

        return _agentContext.AgentId;
    }

    private async Task<Guid> GetOrCreateCoordinatorAuditAgentIdAsync(DateTimeOffset now)
    {
        var existingAgentId = await _db.Agents
            .Where(agent =>
                agent.OrganizationId == _agentContext.OrganizationId &&
                agent.Name == CoordinatorAuditAgentName &&
                agent.Type == AgentType.Orchestrator &&
                agent.AgentPlatform == AgentPlatform.Custom &&
                agent.Status == AgentStatus.Inactive &&
                agent.ApiKeyHash == null)
            .Select(agent => (Guid?)agent.Id)
            .FirstOrDefaultAsync();

        if (existingAgentId.HasValue)
            return existingAgentId.Value;

        var coordinatorAuditAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = _agentContext.OrganizationId,
            Name = CoordinatorAuditAgentName,
            Type = AgentType.Orchestrator,
            AgentPlatform = AgentPlatform.Custom,
            ApiKeyHash = null,
            Status = AgentStatus.Inactive,
            CreatedAt = now
        };

        _db.Agents.Add(coordinatorAuditAgent);
        return coordinatorAuditAgent.Id;
    }

    private string GetCurrentActorName()
    {
        if (_agentContext.IsCoordinator)
            return CoordinatorAuditAgentName;

        return string.IsNullOrWhiteSpace(_agentContext.AgentName)
            ? "Unknown agent"
            : _agentContext.AgentName;
    }

    private AgentType GetCurrentActorType() =>
        _agentContext.IsCoordinator
            ? AgentType.Orchestrator
            : _agentContext.AgentType;

    private static bool TryParseApiEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim();

        return Enum.TryParse(normalized, ignoreCase: true, out parsed);
    }

    private static string ToApiValue(Enum value) =>
        value.ToString().ToLowerInvariant();

    private static IResult Forbidden(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
}
