using Hiveboard.Api.Auth;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Api.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/agents")
            .WithTags("Agents")
            .RequireAuthorization();

        group.MapPost("/register", RegisterAgent)
            .RequireAuthorization("AdminOnly")
            .WithName("RegisterAgent")
            .WithSummary("Register a new agent")
            .WithDescription("Auth: Admin API Key only. Creates an agent and returns a plaintext API key once.")
            .Produces<RegisterAgentResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListAgents)
            .WithName("ListAgents")
            .WithSummary("List agents")
            .WithDescription("Auth: Any authenticated key. Admin sees all organizations; agents see only their organization.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", GetCurrentAgent)
            .WithName("GetCurrentAgent")
            .WithSummary("Get current agent context")
            .WithDescription("Auth: Any authenticated key. Returns admin context for admin key, or agent identity and assigned tasks for agent keys.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", UpdateAgent)
            .RequireAuthorization("AdminOnly")
            .WithName("UpdateAgent")
            .WithSummary("Update an existing agent")
            .WithDescription("Auth: Admin API Key only. Updates agent name, platform, or status.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeactivateAgent)
            .RequireAuthorization("AdminOnly")
            .WithName("DeactivateAgent")
            .WithSummary("Deactivate an agent")
            .WithDescription("Auth: Admin API Key only. Deactivates an agent and revokes its current API key.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/keys/rotate", RotateAgentKey)
            .RequireAuthorization("AdminOnly")
            .WithName("RotateAgentKey")
            .WithSummary("Rotate an agent API key")
            .WithDescription("Auth: Admin API Key only. Rotates the target agent key and returns the new plaintext key once.")
            .Produces<KeyRotationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> RegisterAgent(
        RegisterAgentRequest request,
        HiveboardDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required" });

        if (!Enum.TryParse<AgentType>(request.Type, ignoreCase: true, out var agentType))
            return Results.BadRequest(new { error = "Invalid agent type. Must be 'orchestrator' or 'worker'" });

        if (!Enum.TryParse<AgentPlatform>(request.Platform?.Replace("-", ""), ignoreCase: true, out var platform))
            return Results.BadRequest(new { error = "Invalid platform. Must be one of: copilot, claude-code, cursor, codex, custom" });

        var orgExists = await db.Organizations.AnyAsync(o => o.Id == request.OrganizationId);
        if (!orgExists)
            return Results.BadRequest(new { error = "Organization not found" });

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

        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        return Results.Created($"/api/v1/agents/{agent.Id}", new RegisterAgentResponse(
            agent.Id,
            plaintextKey,
            agent.Name,
            agent.Type.ToString().ToLowerInvariant()));
    }

    private static async Task<IResult> ListAgents(
        HttpContext httpContext,
        HiveboardDbContext db)
    {
        var agentContext = ResolveAgentContext(httpContext);

        if (agentContext.IsAdmin)
        {
            var allAgents = await db.Agents
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    Type = a.Type.ToString().ToLowerInvariant(),
                    Platform = a.AgentPlatform.ToString().ToLowerInvariant(),
                    Status = a.Status.ToString().ToLowerInvariant(),
                    a.LastSeenAt,
                    a.OrganizationId
                })
                .ToListAsync();

            return Results.Ok(allAgents);
        }

        var agents = await db.Agents
            .Where(a => a.OrganizationId == agentContext.OrganizationId)
            .Select(a => new
            {
                a.Id,
                a.Name,
                Type = a.Type.ToString().ToLowerInvariant(),
                Platform = a.AgentPlatform.ToString().ToLowerInvariant(),
                Status = a.Status.ToString().ToLowerInvariant(),
                a.LastSeenAt
            })
            .ToListAsync();

        return Results.Ok(agents);
    }

    private static async Task<IResult> GetCurrentAgent(
        HttpContext httpContext,
        HiveboardDbContext db)
    {
        var agentContext = ResolveAgentContext(httpContext);

        if (agentContext.IsAdmin)
        {
            return Results.Ok(new
            {
                IsAdmin = true,
                Message = "Authenticated as admin. Use agent API keys to access agent-specific endpoints."
            });
        }

        var agent = await db.Agents
            .Where(a => a.Id == agentContext.AgentId)
            .Select(a => new
            {
                a.Id,
                a.Name,
                Type = a.Type.ToString().ToLowerInvariant(),
                Platform = a.AgentPlatform.ToString().ToLowerInvariant(),
                Status = a.Status.ToString().ToLowerInvariant(),
                a.OrganizationId,
                a.LastSeenAt,
                a.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (agent is null)
            return Results.NotFound(new { error = "Agent not found" });

        var assignedTasks = await db.AgentTasks
            .Where(t => t.AssignedAgentId == agentContext.AgentId &&
                        t.Status != Core.Enums.TaskStatus.Done)
            .Select(t => new
            {
                t.Id,
                t.Title,
                Status = t.Status.ToString(),
                t.ProjectId,
                t.UpdatedAt
            })
            .ToListAsync();

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
            AssignedTasks = assignedTasks
        });
    }

    private static async Task<IResult> UpdateAgent(
        Guid id,
        UpdateAgentRequest request,
        HiveboardDbContext db)
    {
        var agent = await db.Agents.FindAsync(id);
        if (agent is null)
            return Results.NotFound(new { error = "Agent not found" });

        if (!string.IsNullOrWhiteSpace(request.Name))
            agent.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Platform))
        {
            if (!Enum.TryParse<AgentPlatform>(request.Platform.Replace("-", ""), ignoreCase: true, out var platform))
                return Results.BadRequest(new { error = "Invalid platform" });
            agent.AgentPlatform = platform;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<AgentStatus>(request.Status, ignoreCase: true, out var status))
                return Results.BadRequest(new { error = "Invalid status. Must be 'active' or 'inactive'" });
            agent.Status = status;
        }

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            agent.Id,
            agent.Name,
            Type = agent.Type.ToString().ToLowerInvariant(),
            Platform = agent.AgentPlatform.ToString().ToLowerInvariant(),
            Status = agent.Status.ToString().ToLowerInvariant()
        });
    }

    private static async Task<IResult> DeactivateAgent(
        Guid id,
        HiveboardDbContext db)
    {
        var agent = await db.Agents.FindAsync(id);
        if (agent is null)
            return Results.NotFound(new { error = "Agent not found" });

        agent.Status = AgentStatus.Inactive;
        agent.ApiKeyHash = string.Empty; // Revoke key
        await db.SaveChangesAsync();

        return Results.Ok(new { message = $"Agent '{agent.Name}' has been deactivated" });
    }

    private static async Task<IResult> RotateAgentKey(
        Guid id,
        HiveboardDbContext db)
    {
        var agent = await db.Agents.FindAsync(id);
        if (agent is null)
            return Results.NotFound(new { error = "Agent not found" });

        if (agent.Status != AgentStatus.Active)
            return Results.BadRequest(new { error = "Cannot rotate key for inactive agent" });

        var plaintextKey = AdminKeyProvider.GenerateAgentKey();
        var keyHash = AdminKeyProvider.HashKey(plaintextKey);

        agent.ApiKeyHash = keyHash;
        await db.SaveChangesAsync();

        return Results.Ok(new KeyRotationResponse(
            plaintextKey,
            "Agent API key rotated successfully. The old key is immediately invalidated."));
    }

    private static AgentContext ResolveAgentContext(HttpContext httpContext)
    {
        var user = httpContext.User;
        var context = new AgentContext();

        var isAdmin = user.FindFirst("IsAdmin")?.Value;
        context.IsAdmin = isAdmin == "true";

        if (!context.IsAdmin)
        {
            var agentIdClaim = user.FindFirst("AgentId")?.Value;
            if (agentIdClaim is not null && Guid.TryParse(agentIdClaim, out var agentId))
                context.AgentId = agentId;

            context.AgentName = user.FindFirst("AgentName")?.Value ?? string.Empty;

            var typeClaim = user.FindFirst("AgentType")?.Value;
            if (typeClaim is not null && Enum.TryParse<AgentType>(typeClaim, out var agentType))
                context.AgentType = agentType;

            var orgClaim = user.FindFirst("OrganizationId")?.Value;
            if (orgClaim is not null && Guid.TryParse(orgClaim, out var orgId))
                context.OrganizationId = orgId;
        }

        return context;
    }
}

public record UpdateAgentRequest(string? Name, string? Platform, string? Status);
