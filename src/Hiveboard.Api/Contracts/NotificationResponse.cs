namespace Hiveboard.Api.Contracts;

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    Guid TaskId,
    string TaskTitle,
    string Message,
    DateTimeOffset CreatedAt,
    bool IsAcknowledged);
