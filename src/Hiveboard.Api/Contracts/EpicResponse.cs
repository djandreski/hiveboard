namespace Hiveboard.Api.Contracts;

public record EpicResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<EpicTaskSummaryResponse>? Tasks);

public record EpicTaskSummaryResponse(
    Guid Id,
    string Title,
    string Description,
    string Status,
    Guid? AssignedAgentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
