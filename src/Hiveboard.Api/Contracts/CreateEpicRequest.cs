namespace Hiveboard.Api.Contracts;

public record CreateEpicRequest(
    string? Title,
    string? Description);
