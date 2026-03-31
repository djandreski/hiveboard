namespace Hiveboard.Api.Contracts;

public record UpdateTaskRequest(
    string? Title,
    string? Description,
    Guid? EpicId,
    Guid? AssignedAgentId,
    Dictionary<string, string>? Metadata);
