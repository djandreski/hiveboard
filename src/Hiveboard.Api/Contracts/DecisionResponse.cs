namespace Hiveboard.Api.Contracts;

public record DecisionResponse(
    Guid Id,
    Guid ProjectId,
    Guid? TaskId,
    Guid AgentId,
    string AgentName,
    string AgentType,
    string Title,
    string Content,
    string Status,
    DateTimeOffset CreatedAt);
