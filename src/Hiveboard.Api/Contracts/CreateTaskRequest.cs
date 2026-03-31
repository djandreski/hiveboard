namespace Hiveboard.Api.Contracts;

public record CreateTaskRequest(
    string? Title,
    string? Description,
    Guid? EpicId,
    Guid? ParentTaskId,
    Dictionary<string, string>? Metadata);
