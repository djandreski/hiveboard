namespace Hiveboard.Api.Contracts;

public record TaskResponse(
    Guid Id,
    string Title,
    string Status,
    Guid? AssignedAgentId,
    Guid? EpicId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
