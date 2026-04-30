using System.Net;
using System.Net.Http.Json;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Tests;

public class TaskCrudAssignmentIntegrationTests
{
    private const string WorkerAApiKey =
        "hb_sk_task_worker_a_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string WorkerBApiKey =
        "hb_sk_task_worker_b_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SecondaryOrchestratorApiKey =
        "hb_sk_task_orchestrator_b_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ForeignWorkerApiKey =
        "hb_sk_foreign_worker_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task TaskEndpoints_CreateListContextAndAssignmentFlows_WorkAsExpected()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var foreignOrganization = IntegrationTestData.CreateOrganization("Foreign Tasks Org");

        var secondaryOrchestrator = IntegrationTestData.CreateAgent(
            organization.Id,
            "Secondary Orchestrator",
            AgentType.Orchestrator,
            SecondaryOrchestratorApiKey);
        var workerA = IntegrationTestData.CreateAgent(
            organization.Id,
            "Worker A",
            AgentType.Worker,
            WorkerAApiKey);
        var workerB = IntegrationTestData.CreateAgent(
            organization.Id,
            "Worker B",
            AgentType.Worker,
            WorkerBApiKey);
        var foreignWorker = IntegrationTestData.CreateAgent(
            foreignOrganization.Id,
            "Foreign Worker",
            AgentType.Worker,
            ForeignWorkerApiKey);

        var project = IntegrationTestData.CreateProject(organization.Id, "Task Platform Work");
        var epic = IntegrationTestData.CreateEpic(project.Id, "Task Endpoint Epic");

        var now = DateTimeOffset.UtcNow;
        var parentTask = new AgentTask
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Parent Integration Task",
            Description = "Parent task used by integration test.",
            Status = TaskStatusEnum.Backlog,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = now.AddMinutes(-10),
            UpdatedAt = now.AddMinutes(-10)
        };

        await app.SeedAsync(db =>
        {
            db.Organizations.AddRange(organization, foreignOrganization);
            db.Agents.AddRange(secondaryOrchestrator, workerA, workerB, foreignWorker);
            db.Projects.Add(project);
            db.Epics.Add(epic);
            db.AgentTasks.Add(parentTask);
        });

        using var coordinatorClient = app.CreateCoordinatorClient();
        using var workerClient = app.CreateAuthenticatedClient(WorkerAApiKey);

        var createResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/tasks",
            new CreateTaskRequest(
                "Implement task CRUD endpoints",
                "Implement list, detail, and assignment behavior.",
                epic.Id,
                parentTask.Id,
                new Dictionary<string, string> { ["branch"] = "feature/task-crud" }));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(createdTask);

        var taskId = createdTask!.Id;
        Assert.Equal("backlog", createdTask.Status);
        Assert.Equal(epic.Id, createdTask.EpicId);
        Assert.Null(createdTask.AssignedAgentId);

        var backlogList = await workerClient.GetFromJsonAsync<List<TaskResponse>>(
            $"/api/v1/projects/{project.Id}/tasks?status=Backlog&epicId={epic.Id}");

        Assert.NotNull(backlogList);
        Assert.Single(backlogList);
        Assert.Equal(taskId, backlogList[0].Id);

        var nonWorkerAssignmentResponse = await coordinatorClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest(null, null, null, secondaryOrchestrator.Id, null));
        Assert.Equal(HttpStatusCode.BadRequest, nonWorkerAssignmentResponse.StatusCode);

        var foreignWorkerAssignmentResponse = await coordinatorClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest(null, null, null, foreignWorker.Id, null));
        Assert.Equal(HttpStatusCode.BadRequest, foreignWorkerAssignmentResponse.StatusCode);

        var assignResponse = await coordinatorClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest(null, null, null, workerA.Id, null));
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        var assignedTask = await assignResponse.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(assignedTask);
        Assert.Equal("assigned", assignedTask!.Status);
        Assert.Equal(workerA.Id, assignedTask.AssignedAgentId);

        var filteredList = await workerClient.GetFromJsonAsync<List<TaskResponse>>(
            $"/api/v1/projects/{project.Id}/tasks?status=Assigned&agentId={workerA.Id}&epicId={epic.Id}");

        Assert.NotNull(filteredList);
        Assert.Single(filteredList);
        Assert.Equal(taskId, filteredList[0].Id);

        var conflictResponse = await coordinatorClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest(null, null, null, workerB.Id, null));
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        var assignmentArtifacts = await app.QueryAsync(async db => new AssignmentArtifacts(
            await db.TaskEvents.AnyAsync(taskEvent =>
                taskEvent.TaskId == taskId &&
                taskEvent.EventType == "assigned"),
            await db.Notifications.AnyAsync(notification =>
                notification.TaskId == taskId &&
                notification.AgentId == workerA.Id &&
                notification.Type == NotificationType.TaskAssigned)));

        Assert.True(assignmentArtifacts.HasAssignedEvent);
        Assert.True(assignmentArtifacts.HasTaskAssignmentNotification);

        var contextFixtureIds = await app.QueryAsync(async db =>
        {
            var contextNow = DateTimeOffset.UtcNow;

            var blockedByTask = new AgentTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Title = "Blocked By Task",
                Description = "Dependency predecessor",
                Status = TaskStatusEnum.Done,
                Metadata = new Dictionary<string, string>(),
                CreatedAt = contextNow.AddMinutes(-5),
                UpdatedAt = contextNow.AddMinutes(-5)
            };

            var blockingTask = new AgentTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Title = "Blocking Task",
                Description = "Depends on created task",
                Status = TaskStatusEnum.Backlog,
                Metadata = new Dictionary<string, string>(),
                CreatedAt = contextNow.AddMinutes(-4),
                UpdatedAt = contextNow.AddMinutes(-4)
            };

            var subtask = new AgentTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                ParentTaskId = taskId,
                Title = "Subtask for integration test",
                Description = "Subtask context",
                Status = TaskStatusEnum.Assigned,
                AssignedAgentId = workerA.Id,
                Metadata = new Dictionary<string, string>(),
                CreatedAt = contextNow.AddMinutes(-3),
                UpdatedAt = contextNow.AddMinutes(-3)
            };

            db.AgentTasks.AddRange(blockedByTask, blockingTask, subtask);
            var blockedByDependency = new TaskDependency
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                DependsOnTaskId = blockedByTask.Id,
                Type = DependencyType.Blocks
            };
            var blockingDependency = new TaskDependency
            {
                Id = Guid.NewGuid(),
                TaskId = blockingTask.Id,
                DependsOnTaskId = taskId,
                Type = DependencyType.Blocks
            };

            db.TaskDependencies.AddRange(blockedByDependency, blockingDependency);
            db.TaskNotes.Add(new TaskNote
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                AgentId = workerA.Id,
                Content = "Initial implementation context is available.",
                NoteType = NoteType.Context,
                CreatedAt = contextNow.AddMinutes(-2)
            });
            db.TaskEvents.Add(new TaskEvent
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                AgentId = workerA.Id,
                EventType = "progress-update",
                OldValue = "assigned",
                NewValue = "in-progress",
                Timestamp = contextNow.AddMinutes(-1)
            });

            var decision = new DecisionRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TaskId = taskId,
                AgentId = secondaryOrchestrator.Id,
                Title = "Use minimal API handlers",
                Content = "Keeps endpoint definitions concise.",
                Status = DecisionStatus.Accepted,
                CreatedAt = contextNow
            };

            db.DecisionRecords.Add(decision);
            await db.SaveChangesAsync();

            return new TaskContextFixtureIds(
                blockedByTask.Id,
                blockedByDependency.Id,
                blockingTask.Id,
                blockingDependency.Id,
                subtask.Id,
                decision.Id);
        });

        var detailResponse = await workerClient.GetAsync($"/api/v1/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        var detail = await detailResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();
        Assert.NotNull(detail);

        var taskDetail = detail!.Task;
        Assert.Equal(taskId, taskDetail.Id);
        Assert.Equal(parentTask.Id, taskDetail.ParentTaskId);
        Assert.Equal(epic.Id, taskDetail.EpicId);
        Assert.Equal(workerA.Id, taskDetail.AssignedAgentId);

        Assert.NotNull(detail.Epic);
        Assert.Equal(epic.Id, detail.Epic!.Id);

        Assert.NotNull(detail.ParentTask);
        Assert.Equal(parentTask.Id, detail.ParentTask!.Id);

        Assert.NotNull(detail.Project);
        Assert.Equal(project.Id, detail.Project.Id);
        Assert.Equal(project.Name, detail.Project.Name);

        Assert.Contains(detail.Subtasks, subtask =>
            subtask.Id == contextFixtureIds.SubtaskId &&
            subtask.AssignedAgentName == workerA.Name);
        Assert.Contains(detail.Dependencies.BlockedBy, dependency =>
            dependency.TaskId == contextFixtureIds.BlockedByTaskId &&
            dependency.DepId == contextFixtureIds.BlockedByDependencyId);
        Assert.Contains(detail.Dependencies.Blocking, dependency =>
            dependency.TaskId == contextFixtureIds.BlockingTaskId &&
            dependency.DepId == contextFixtureIds.BlockingDependencyId);
        Assert.Contains(detail.Notes, note =>
            note.Agent == workerA.Name &&
            note.AgentType == "worker" &&
            note.Type == "context");
        Assert.Contains(detail.Events, taskEvent => taskEvent.EventType == "assigned");
        Assert.Contains(detail.RelatedDecisions, decision => decision.Id == contextFixtureIds.DecisionId);
    }

    private sealed record AssignmentArtifacts(
        bool HasAssignedEvent,
        bool HasTaskAssignmentNotification);

    private sealed record TaskContextFixtureIds(
        Guid BlockedByTaskId,
        Guid BlockedByDependencyId,
        Guid BlockingTaskId,
        Guid BlockingDependencyId,
        Guid SubtaskId,
        Guid DecisionId);
}
