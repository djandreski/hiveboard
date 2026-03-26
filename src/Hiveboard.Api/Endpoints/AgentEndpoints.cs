using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;

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

    private static Task<IResult> RegisterAgent(
        RegisterAgentRequest request,
        AgentApplicationService applicationService)
        => applicationService.RegisterAgentAsync(request);

    private static Task<IResult> ListAgents(AgentApplicationService applicationService)
        => applicationService.ListAgentsAsync();

    private static Task<IResult> GetCurrentAgent(AgentApplicationService applicationService)
        => applicationService.GetCurrentAgentAsync();

    private static Task<IResult> UpdateAgent(
        Guid id,
        UpdateAgentRequest request,
        AgentApplicationService applicationService)
        => applicationService.UpdateAgentAsync(id, request);

    private static Task<IResult> DeactivateAgent(
        Guid id,
        AgentApplicationService applicationService)
        => applicationService.DeactivateAgentAsync(id);

    private static Task<IResult> RotateAgentKey(
        Guid id,
        AgentApplicationService applicationService)
        => applicationService.RotateAgentKeyAsync(id);
}
