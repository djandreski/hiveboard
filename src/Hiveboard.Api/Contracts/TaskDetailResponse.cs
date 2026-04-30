namespace Hiveboard.Api.Contracts;

public record TaskDetailResponse(
    TaskContextTaskResponse Task,
    TaskContextProjectResponse Project,
    TaskContextEpicResponse? Epic,
    TaskContextParentTaskResponse? ParentTask,
    IReadOnlyList<TaskContextSubtaskResponse> Subtasks,
    TaskContextDependenciesResponse Dependencies,
    IReadOnlyList<TaskContextNoteResponse> Notes,
    IReadOnlyList<TaskContextEventResponse> Events,
    IReadOnlyList<TaskContextDecisionResponse> RelatedDecisions);

public record TaskContextTaskResponse(
    Guid Id,
    Guid ProjectId,
    Guid? EpicId,
    Guid? ParentTaskId,
    Guid? AssignedAgentId,
    string Title,
    string Description,
    string Status,
    string? BlockedReason,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record TaskContextProjectResponse(Guid Id, string Name);

public record TaskContextEpicResponse(
    Guid Id,
    string Title,
    string Description,
    string Status);

public record TaskContextParentTaskResponse(
    Guid Id,
    string Title,
    string Status);

public record TaskContextSubtaskResponse(
    Guid Id,
    string Title,
    string Status,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    DateTimeOffset UpdatedAt);

public record TaskContextDependenciesResponse(
    IReadOnlyList<TaskContextDependencyTaskResponse> BlockedBy,
    IReadOnlyList<TaskContextDependencyTaskResponse> Blocking);

public record TaskContextDependencyTaskResponse(
    Guid TaskId,
    string Title,
    string Status,
    Guid DepId);

public record TaskContextNoteResponse(
    string Agent,
    string AgentType,
    string Type,
    string Content,
    DateTimeOffset CreatedAt);

public record TaskContextEventResponse(
    Guid Id,
    string EventType,
    string? OldValue,
    string? NewValue,
    string Agent,
    DateTimeOffset Timestamp);

public record TaskContextDecisionResponse(
    Guid Id,
    string Title,
    string Content,
    string Status,
    string Agent,
    DateTimeOffset CreatedAt);
