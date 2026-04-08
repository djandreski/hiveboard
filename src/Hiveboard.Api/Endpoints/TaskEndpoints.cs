using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Tasks")
            .RequireAuthorization();

        group.MapGet("/projects/{projectId:guid}/tasks", ListProjectTasks)
            .WithName("ListProjectTasks")
            .WithSummary("List tasks for a project")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Lists project tasks and supports optional status, agentId, and epicId filters.")
            .Produces<List<TaskResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/projects/{projectId:guid}/tasks", CreateTask)
            .RequireAuthorization("CoordinatorOrOrchestratorOnly")
            .WithName("CreateTask")
            .WithSummary("Create a task")
            .WithDescription("Auth: Coordinator/admin key or orchestrator agent API key. Creates a backlog task under the selected project.")
            .Produces<TaskResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/tasks/{id:guid}", GetTaskById)
            .WithName("GetTaskById")
            .WithSummary("Get full task context")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Returns full task context including relationships, notes, events, and decisions.")
            .Produces<TaskDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/tasks/{id:guid}", UpdateTask)
            .RequireAuthorization("CoordinatorOrOrchestratorOnly")
            .WithName("UpdateTask")
            .WithSummary("Update task metadata or assignment")
            .WithDescription("Auth: Coordinator/admin key or orchestrator agent API key. Updates task fields and handles assignment side-effects.")
            .Produces<TaskResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    private static Task<IResult> ListProjectTasks(
        Guid projectId,
        string? status,
        Guid? agentId,
        Guid? epicId,
        TaskApplicationService applicationService)
        => applicationService.ListProjectTasksAsync(projectId, status, agentId, epicId);

    private static Task<IResult> CreateTask(
        Guid projectId,
        CreateTaskRequest? request,
        TaskApplicationService applicationService)
        => applicationService.CreateTaskAsync(projectId, request);

    private static Task<IResult> GetTaskById(
        Guid id,
        TaskApplicationService applicationService)
        => applicationService.GetTaskByIdAsync(id);

    private static Task<IResult> UpdateTask(
        Guid id,
        UpdateTaskRequest? request,
        TaskApplicationService applicationService)
        => applicationService.UpdateTaskAsync(id, request);
}
