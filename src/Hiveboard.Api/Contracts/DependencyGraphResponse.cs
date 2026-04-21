namespace Hiveboard.Api.Contracts;

public record TaskDependencyResponse(
    Guid Id,
    Guid TaskId,
    Guid DependsOnTaskId,
    string Type);

public record DependencyGraphResponse(
    IReadOnlyList<DependencyGraphNodeResponse> Nodes,
    IReadOnlyList<DependencyGraphEdgeResponse> Edges);

public record DependencyGraphNodeResponse(
    Guid TaskId,
    string Title,
    string Status,
    Guid? AssignedAgentId);

public record DependencyGraphEdgeResponse(
    Guid From,
    Guid To,
    string Type,
    Guid DepId);
