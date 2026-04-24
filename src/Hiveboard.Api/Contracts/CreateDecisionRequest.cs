namespace Hiveboard.Api.Contracts;

public record CreateDecisionRequest(
    string? Title,
    string? Content,
    Guid? TaskId,
    string? Status);
