namespace Hiveboard.Api.Contracts;

public record CreateProjectRequest(
    string? Name,
    string? Description);
