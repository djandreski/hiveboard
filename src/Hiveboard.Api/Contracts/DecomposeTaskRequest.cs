namespace Hiveboard.Api.Contracts;

public record DecomposeTaskRequest(
    IReadOnlyList<DecomposeTaskSubtaskRequest>? Subtasks);

public record DecomposeTaskSubtaskRequest(
    string? Title,
    string? Description);
