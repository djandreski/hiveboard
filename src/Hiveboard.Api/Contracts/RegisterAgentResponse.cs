namespace Hiveboard.Api.Contracts;

public record RegisterAgentResponse(
    Guid AgentId,
    string ApiKey,
    string Name,
    string Type);
