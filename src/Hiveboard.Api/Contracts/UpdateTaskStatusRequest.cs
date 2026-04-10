namespace Hiveboard.Api.Contracts;

public record UpdateTaskStatusRequest(
    string? Status,
    string? BlockedReason,
    Guid? AssignedAgentId);
