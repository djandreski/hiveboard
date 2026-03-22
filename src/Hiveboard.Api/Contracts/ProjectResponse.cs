namespace Hiveboard.Api.Contracts;

public record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt);
