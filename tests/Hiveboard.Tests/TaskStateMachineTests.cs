using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Core.Services;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Tests;

public class TaskStateMachineTests
{
    private readonly TaskStateMachine _stateMachine = new();

    [Fact]
    public void BacklogToAssigned_Succeeds_ForCoordinator_WhenWorkerIsAssigned()
    {
        var task = CreateTask(TaskStatusEnum.Backlog, assignedAgentId: Guid.NewGuid());
        var caller = CreateCoordinator();

        var result = _stateMachine.ValidateTransition(task, TaskStatusEnum.Assigned, caller);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void AssignedToInProgress_Fails_WhenDependenciesAreNotDone()
    {
        var workerId = Guid.NewGuid();
        var dependencyTask = CreateTask(TaskStatusEnum.Blocked, title: "Dependency");
        var task = CreateTask(TaskStatusEnum.Assigned, assignedAgentId: workerId);
        task.Dependencies.Add(new TaskDependency
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            DependsOnTaskId = dependencyTask.Id,
            DependsOnTask = dependencyTask,
            Type = DependencyType.Blocks
        });

        var result = _stateMachine.ValidateTransition(task, TaskStatusEnum.InProgress, CreateWorker(workerId));

        Assert.False(result.IsSuccess);
        Assert.Equal(TaskTransitionFailureKind.DependencyViolation, result.FailureKind);
        Assert.Single(result.UnmetDependencies);
        Assert.Equal(dependencyTask.Id, result.UnmetDependencies[0].TaskId);
    }

    [Fact]
    public void InProgressToBlocked_Fails_WhenReasonIsMissing()
    {
        var workerId = Guid.NewGuid();
        var task = CreateTask(TaskStatusEnum.InProgress, assignedAgentId: workerId);

        var result = _stateMachine.ValidateTransition(task, TaskStatusEnum.Blocked, CreateWorker(workerId));

        Assert.False(result.IsSuccess);
        Assert.Equal(TaskTransitionFailureKind.InvalidTransition, result.FailureKind);
        Assert.Equal("Transitioning to 'blocked' requires a blocked reason.", result.ErrorMessage);
    }

    [Fact]
    public void ConfiguredOrchestrator_CanApproveInReviewTask()
    {
        var orchestratorId = Guid.NewGuid();
        var task = CreateTask(TaskStatusEnum.InReview);
        task.Project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            OrchestratorAgentId = orchestratorId,
            Name = "Project",
            Description = string.Empty,
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = _stateMachine.ValidateTransition(task, TaskStatusEnum.Done, CreateOrchestrator(orchestratorId));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void UnconfiguredOrchestrator_CannotPerformCoordinatorTransition()
    {
        var task = CreateTask(TaskStatusEnum.InReview);
        task.Project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            OrchestratorAgentId = Guid.NewGuid(),
            Name = "Project",
            Description = string.Empty,
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = _stateMachine.ValidateTransition(task, TaskStatusEnum.Done, CreateOrchestrator(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(TaskTransitionFailureKind.Forbidden, result.FailureKind);
    }

    [Fact]
    public void AnyToBacklog_Fails_WhenAssignmentIsNotCleared()
    {
        var task = CreateTask(TaskStatusEnum.Done, assignedAgentId: Guid.NewGuid());

        var result = _stateMachine.ValidateTransition(task, TaskStatusEnum.Backlog, CreateCoordinator());

        Assert.False(result.IsSuccess);
        Assert.Equal(TaskTransitionFailureKind.InvalidTransition, result.FailureKind);
        Assert.Equal("Transitioning to 'backlog' must clear the assigned agent.", result.ErrorMessage);
    }

    private static AgentTask CreateTask(
        TaskStatusEnum status,
        Guid? assignedAgentId = null,
        string title = "Task") =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            AssignedAgentId = assignedAgentId,
            Title = title,
            Description = string.Empty,
            Status = status,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static AgentContext CreateCoordinator() =>
        new()
        {
            IsAdmin = true,
            OrganizationId = Guid.NewGuid()
        };

    private static AgentContext CreateWorker(Guid workerId) =>
        new()
        {
            AgentId = workerId,
            AgentType = AgentType.Worker,
            OrganizationId = Guid.NewGuid()
        };

    private static AgentContext CreateOrchestrator(Guid orchestratorId) =>
        new()
        {
            AgentId = orchestratorId,
            AgentType = AgentType.Orchestrator,
            OrganizationId = Guid.NewGuid()
        };
}
