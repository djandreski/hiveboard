using Hiveboard.Api.Auth;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Core.Services;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Api.Application;

public sealed class AgentApplicationService
{
    private const string CoordinatorAuditAgentName = "Coordinator";

    private readonly HiveboardDbContext _db;
    private readonly AgentContext _agentContext;
    private readonly IAgentAccessGuard _accessGuard;

    public AgentApplicationService(
        HiveboardDbContext db,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard)
    {
        _db = db;
        _agentContext = agentContext;
        _accessGuard = accessGuard;
    }

    public async Task<IResult> RegisterAgentAsync(RegisterAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required");

        if (!Enum.TryParse<AgentType>(request.Type, ignoreCase: true, out var agentType))
            return BadRequest("Invalid agent type. Must be 'orchestrator' or 'worker'");

        if (!Enum.TryParse<AgentPlatform>(request.Platform?.Replace("-", ""), ignoreCase: true, out var platform))
            return BadRequest("Invalid platform. Must be one of: copilot, claude-code, cursor, codex, custom");

        var orgExists = await _db.Organizations.AnyAsync(organization => organization.Id == request.OrganizationId);
        if (!orgExists)
            return BadRequest("Organization not found");

        var plaintextKey = AdminKeyProvider.GenerateAgentKey();
        var keyHash = AdminKeyProvider.HashKey(plaintextKey);

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = request.Name,
            Type = agentType,
            AgentPlatform = platform,
            ApiKeyHash = keyHash,
            Status = AgentStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Agents.Add(agent);
        await _db.SaveChangesAsync();

        return Results.Created($"/api/v1/agents/{agent.Id}", new RegisterAgentResponse(
            agent.Id,
            plaintextKey,
            agent.Name,
            agent.Type.ToString().ToLowerInvariant()));
    }

    public async Task<IResult> ListAgentsAsync()
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var organizationAgents = await _db.Agents
            .Where(agent =>
                agent.OrganizationId == _agentContext.OrganizationId &&
                !(agent.Name == CoordinatorAuditAgentName &&
                  agent.Type == AgentType.Orchestrator &&
                  agent.AgentPlatform == AgentPlatform.Custom &&
                  agent.Status == AgentStatus.Inactive &&
                  agent.ApiKeyHash == null))
            .Select(agent => new
            {
                agent.Id,
                agent.Name,
                Type = agent.Type.ToString().ToLowerInvariant(),
                Platform = agent.AgentPlatform.ToString().ToLowerInvariant(),
                Status = agent.Status.ToString().ToLowerInvariant(),
                agent.LastSeenAt
            })
            .ToListAsync();

        return Results.Ok(organizationAgents);
    }

    public async Task<IResult> GetCurrentAgentAsync()
    {
        if (_agentContext.IsCoordinator)
        {
            return Results.Ok(new
            {
                IsAdmin = true,
                IsCoordinator = true,
                OrganizationId = _agentContext.HasOrganizationScope ? (Guid?)_agentContext.OrganizationId : null,
                _agentContext.OrganizationScopeError,
                Message = _agentContext.HasOrganizationScope
                    ? "Authenticated as coordinator/admin."
                    : "Authenticated as coordinator/admin, but no organization scope could be resolved."
            });
        }

        var agent = await _db.Agents
            .Where(candidate => candidate.Id == _agentContext.AgentId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                Type = candidate.Type.ToString().ToLowerInvariant(),
                Platform = candidate.AgentPlatform.ToString().ToLowerInvariant(),
                Status = candidate.Status.ToString().ToLowerInvariant(),
                candidate.OrganizationId,
                candidate.LastSeenAt,
                candidate.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (agent is null)
            return Results.NotFound(new { error = "Agent not found" });

        var assignedTasksRaw = await _db.AgentTasks
            .Where(task => task.AssignedAgentId == _agentContext.AgentId &&
                           task.Status != TaskStatusEnum.Done)
            .Select(task => new
            {
                task.Id,
                task.Title,
                Status = task.Status.ToString().ToLowerInvariant(),
                task.ProjectId,
                task.UpdatedAt
            })
            .ToListAsync();

        var assignedTasks = assignedTasksRaw
            .OrderBy(task => task.UpdatedAt)
            .ToList();

        var unacknowledgedNotificationCount = await _db.Notifications
            .CountAsync(notification =>
                notification.AgentId == _agentContext.AgentId &&
                !notification.IsAcknowledged);

        return Results.Ok(new
        {
            agent.Id,
            agent.Name,
            agent.Type,
            agent.Platform,
            agent.Status,
            agent.OrganizationId,
            agent.LastSeenAt,
            agent.CreatedAt,
            AssignedTasks = assignedTasks,
            UnacknowledgedNotificationCount = unacknowledgedNotificationCount
        });
    }

    public async Task<IResult> UpdateAgentAsync(Guid id, UpdateAgentRequest request)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent is null)
            return Results.NotFound(new { error = "Agent not found" });

        if (!string.IsNullOrWhiteSpace(request.Name))
            agent.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Platform))
        {
            if (!Enum.TryParse<AgentPlatform>(request.Platform.Replace("-", ""), ignoreCase: true, out var platform))
                return BadRequest("Invalid platform");

            agent.AgentPlatform = platform;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<AgentStatus>(request.Status, ignoreCase: true, out var status))
                return BadRequest("Invalid status. Must be 'active' or 'inactive'");

            if (status == AgentStatus.Inactive)
            {
                AgentKeyLifecycle.Revoke(agent);
            }
            else if (AgentKeyLifecycle.RequiresNewKeyToActivate(agent))
            {
                return BadRequest("Cannot reactivate an inactive agent without issuing a new API key. Use POST /api/v1/agents/{id}/keys/rotate.");
            }
            else
            {
                agent.Status = status;
            }
        }

        await _db.SaveChangesAsync();

        return Results.Ok(new
        {
            agent.Id,
            agent.Name,
            Type = agent.Type.ToString().ToLowerInvariant(),
            Platform = agent.AgentPlatform.ToString().ToLowerInvariant(),
            Status = agent.Status.ToString().ToLowerInvariant()
        });
    }

    public async Task<IResult> DeactivateAgentAsync(Guid id)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent is null)
            return Results.NotFound(new { error = "Agent not found" });

        AgentKeyLifecycle.Revoke(agent);
        await _db.SaveChangesAsync();

        return Results.Ok(new { message = $"Agent '{agent.Name}' has been deactivated" });
    }

    public async Task<IResult> RotateAgentKeyAsync(Guid id)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent is null)
            return Results.NotFound(new { error = "Agent not found" });

        var plaintextKey = AdminKeyProvider.GenerateAgentKey();
        var keyHash = AdminKeyProvider.HashKey(plaintextKey);

        AgentKeyLifecycle.IssueNewKey(agent, keyHash);
        await _db.SaveChangesAsync();

        return Results.Ok(new KeyRotationResponse(
            plaintextKey,
            "Agent API key issued successfully. Any previously active key is immediately invalidated."));
    }

    private static IResult BadRequest(string error) =>
        Results.BadRequest(new { error });
}
