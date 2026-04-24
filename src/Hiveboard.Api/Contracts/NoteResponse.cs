namespace Hiveboard.Api.Contracts;

public record NoteResponse(
    Guid Id,
    Guid TaskId,
    Guid AgentId,
    string AgentName,
    string AgentType,
    string Content,
    string NoteType,
    DateTimeOffset CreatedAt);
