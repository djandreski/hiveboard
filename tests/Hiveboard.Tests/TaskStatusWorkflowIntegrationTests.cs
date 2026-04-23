using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Tests;

public class TaskStatusWorkflowIntegrationTests
{
    private const string WorkerAApiKey =
        "hb_sk_status_worker_a_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string WorkerBApiKey =
        "hb_sk_status_worker_b_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OrchestratorApiKey =
        "hb_sk_status_orchestrator_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task StatusEndpoint_EnforcesDependencies_AndCreatesReviewNotifications()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var worker = IntegrationTestData.CreateAgent(organization.Id, "Worker A", AgentType.Worker, WorkerAApiKey);
        var orchestrator = IntegrationTestData.CreateAgent(organization.Id, "Review Orchestrator", AgentType.Orchestrator, OrchestratorApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Workflow Project");
        project.OrchestratorAgentId = orchestrator.Id;

        var blockingTask = CreateTask(project.Id, "Dependency", TaskStatusEnum.Blocked);
        var task = CreateTask(project.Id, "Review Me", TaskStatusEnum.Assigned, worker.Id);

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.AddRange(worker, orchestrator);
            db.Projects.Add(project);
            db.AgentTasks.AddRange(blockingTask, task);
            db.TaskDependencies.Add(new TaskDependency
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                DependsOnTaskId = blockingTask.Id,
                Type = DependencyType.Blocks
            });
        });

        using var workerClient = app.CreateAuthenticatedClient(WorkerAApiKey);

        var dependencyFailure = await workerClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/status",
            new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.Conflict, dependencyFailure.StatusCode);

        using (var dependencyFailurePayload = JsonDocument.Parse(await dependencyFailure.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Dependency", dependencyFailurePayload.RootElement
                .GetProperty("unmetDependencies")[0]
                .GetProperty("title")
                .GetString());
        }

        await app.QueryAsync(async db =>
        {
            var seededDependency = await db.AgentTasks.SingleAsync(candidate => candidate.Id == blockingTask.Id);
            seededDependency.Status = TaskStatusEnum.Done;
            seededDependency.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return true;
        });

        var startResponse = await workerClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/status",
            new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        var reviewResponse = await workerClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/status",
            new { status = "InReview" });
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);

        var workflowArtifacts = await app.QueryAsync(async db =>
        {
            var coordinatorNotificationExists = await db.Notifications
                .Include(notification => notification.Agent)
                .AnyAsync(notification =>
                    notification.TaskId == task.Id &&
                    notification.Type == NotificationType.ReviewRequested &&
                    notification.Agent != null &&
                    notification.Agent.Name == "Coordinator");

            var orchestratorNotificationExists = await db.Notifications
                .AnyAsync(notification =>
                    notification.TaskId == task.Id &&
                    notification.Type == NotificationType.ReviewRequested &&
                    notification.AgentId == orchestrator.Id);

            var statusEvents = await db.TaskEvents
                .Where(taskEvent => taskEvent.TaskId == task.Id && taskEvent.EventType == "status_changed")
                .Select(taskEvent => new { taskEvent.OldValue, taskEvent.NewValue, taskEvent.Timestamp })
                .ToListAsync();

            statusEvents = statusEvents
                .OrderBy(taskEvent => taskEvent.Timestamp)
                .Select(taskEvent => new { taskEvent.OldValue, taskEvent.NewValue, taskEvent.Timestamp })
                .ToList();

            var taskStatus = await db.AgentTasks
                .Where(candidate => candidate.Id == task.Id)
                .Select(candidate => candidate.Status)
                .SingleAsync();

            return new
            {
                coordinatorNotificationExists,
                orchestratorNotificationExists,
                statusEvents,
                taskStatus
            };
        });

        Assert.True(workflowArtifacts.coordinatorNotificationExists);
        Assert.True(workflowArtifacts.orchestratorNotificationExists);
        Assert.Equal(TaskStatusEnum.InReview, workflowArtifacts.taskStatus);
        Assert.Contains(workflowArtifacts.statusEvents, taskEvent => taskEvent.OldValue == "assigned" && taskEvent.NewValue == "inprogress");
        Assert.Contains(workflowArtifacts.statusEvents, taskEvent => taskEvent.OldValue == "inprogress" && taskEvent.NewValue == "inreview");
    }

    [Fact]
    public async Task StatusEndpoint_RejectsOtherWorkers_AndAllowsConfiguredOrchestratorToUnblock()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var workerA = IntegrationTestData.CreateAgent(organization.Id, "Worker A", AgentType.Worker, WorkerAApiKey);
        var workerB = IntegrationTestData.CreateAgent(organization.Id, "Worker B", AgentType.Worker, WorkerBApiKey);
        var orchestrator = IntegrationTestData.CreateAgent(organization.Id, "Optional Orchestrator", AgentType.Orchestrator, OrchestratorApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Blocked Project");
        project.OrchestratorAgentId = orchestrator.Id;

        var task = CreateTask(project.Id, "Blocked Task", TaskStatusEnum.InProgress, workerA.Id);

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.AddRange(workerA, workerB, orchestrator);
            db.Projects.Add(project);
            db.AgentTasks.Add(task);
        });

        using var workerAClient = app.CreateAuthenticatedClient(WorkerAApiKey);
        using var workerBClient = app.CreateAuthenticatedClient(WorkerBApiKey);
        using var orchestratorClient = app.CreateAuthenticatedClient(OrchestratorApiKey);

        var forbiddenResponse = await workerBClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/status",
            new { status = "Blocked", blockedReason = "Should not be allowed" });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        var blockedResponse = await workerAClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/status",
            new { status = "Blocked", blockedReason = "Waiting on external API" });
        Assert.Equal(HttpStatusCode.OK, blockedResponse.StatusCode);

        var unblockResponse = await orchestratorClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/status",
            new { status = "Assigned" });
        Assert.Equal(HttpStatusCode.OK, unblockResponse.StatusCode);

        var blockedArtifacts = await app.QueryAsync(async db =>
        {
            var updatedTask = await db.AgentTasks.SingleAsync(candidate => candidate.Id == task.Id);

            var coordinatorNotificationExists = await db.Notifications
                .Include(notification => notification.Agent)
                .AnyAsync(notification =>
                    notification.TaskId == task.Id &&
                    notification.Type == NotificationType.TaskBlocked &&
                    notification.Message.Contains("Waiting on external API") &&
                    notification.Agent != null &&
                    notification.Agent.Name == "Coordinator");

            var orchestratorNotificationExists = await db.Notifications
                .AnyAsync(notification =>
                    notification.TaskId == task.Id &&
                    notification.Type == NotificationType.TaskBlocked &&
                    notification.AgentId == orchestrator.Id);

            return new
            {
                updatedTask.Status,
                updatedTask.BlockedReason,
                coordinatorNotificationExists,
                orchestratorNotificationExists
            };
        });

        Assert.Equal(TaskStatusEnum.Assigned, blockedArtifacts.Status);
        Assert.Null(blockedArtifacts.BlockedReason);
        Assert.True(blockedArtifacts.coordinatorNotificationExists);
        Assert.True(blockedArtifacts.orchestratorNotificationExists);
    }

    [Fact]
    public async Task StatusEndpoint_DoneApproval_NotifiesDependents_AndAutoCompletesParent()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var workerA = IntegrationTestData.CreateAgent(organization.Id, "Worker A", AgentType.Worker, WorkerAApiKey);
        var workerB = IntegrationTestData.CreateAgent(organization.Id, "Worker B", AgentType.Worker, WorkerBApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Done Project");

        var parentTask = CreateTask(project.Id, "Parent Task", TaskStatusEnum.InProgress);
        var completedSibling = CreateTask(project.Id, "Completed Sibling", TaskStatusEnum.Done, workerA.Id, parentTask.Id);
        var reviewTask = CreateTask(project.Id, "Review Task", TaskStatusEnum.InReview, workerA.Id, parentTask.Id);
        var dependentTask = CreateTask(project.Id, "Dependent Task", TaskStatusEnum.Assigned, workerB.Id);

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.AddRange(workerA, workerB);
            db.Projects.Add(project);
            db.AgentTasks.AddRange(parentTask, completedSibling, reviewTask, dependentTask);
            db.TaskDependencies.Add(new TaskDependency
            {
                Id = Guid.NewGuid(),
                TaskId = dependentTask.Id,
                DependsOnTaskId = reviewTask.Id,
                Type = DependencyType.Blocks
            });
        });

        using var coordinatorClient = app.CreateCoordinatorClient();

        var doneResponse = await coordinatorClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{reviewTask.Id}/status",
            new { status = "Done" });
        Assert.Equal(HttpStatusCode.OK, doneResponse.StatusCode);

        var completionArtifacts = await app.QueryAsync(async db =>
        {
            var childStatus = await db.AgentTasks
                .Where(candidate => candidate.Id == reviewTask.Id)
                .Select(candidate => candidate.Status)
                .SingleAsync();

            var parentStatus = await db.AgentTasks
                .Where(candidate => candidate.Id == parentTask.Id)
                .Select(candidate => candidate.Status)
                .SingleAsync();

            var dependencyResolvedExists = await db.Notifications
                .AnyAsync(notification =>
                    notification.TaskId == dependentTask.Id &&
                    notification.AgentId == workerB.Id &&
                    notification.Type == NotificationType.DependencyResolved);

            var parentStatusChangedEventExists = await db.TaskEvents
                .AnyAsync(taskEvent =>
                    taskEvent.TaskId == parentTask.Id &&
                    taskEvent.EventType == "status_changed" &&
                    taskEvent.NewValue == "done");

            return new
            {
                childStatus,
                parentStatus,
                dependencyResolvedExists,
                parentStatusChangedEventExists
            };
        });

        Assert.Equal(TaskStatusEnum.Done, completionArtifacts.childStatus);
        Assert.Equal(TaskStatusEnum.Done, completionArtifacts.parentStatus);
        Assert.True(completionArtifacts.dependencyResolvedExists);
        Assert.True(completionArtifacts.parentStatusChangedEventExists);
    }

    [Fact]
    public async Task StatusEndpoint_RejectsDirectCompletion_WhenTaskHasSubtasks()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var worker = IntegrationTestData.CreateAgent(organization.Id, "Worker A", AgentType.Worker, WorkerAApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Parent Guard Project");

        var parentTask = CreateTask(project.Id, "Parent Task", TaskStatusEnum.InProgress, worker.Id);
        var subtask = CreateTask(project.Id, "Child Task", TaskStatusEnum.Backlog, parentTaskId: parentTask.Id);

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.Add(worker);
            db.Projects.Add(project);
            db.AgentTasks.AddRange(parentTask, subtask);
        });

        using var workerClient = app.CreateAuthenticatedClient(WorkerAApiKey);

        var response = await workerClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{parentTask.Id}/status",
            new { status = "Done" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using (var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                "Tasks with subtasks cannot be completed directly. Complete all subtasks instead.",
                payload.RootElement.GetProperty("error").GetString());
        }

        var persistedStatus = await app.QueryAsync(db =>
            db.AgentTasks
                .Where(candidate => candidate.Id == parentTask.Id)
                .Select(candidate => candidate.Status)
                .SingleAsync());

        Assert.Equal(TaskStatusEnum.InProgress, persistedStatus);
    }

    private static AgentTask CreateTask(
        Guid projectId,
        string title,
        TaskStatusEnum status,
        Guid? assignedAgentId = null,
        Guid? parentTaskId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ParentTaskId = parentTaskId,
            AssignedAgentId = assignedAgentId,
            Title = title,
            Description = string.Empty,
            Status = status,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}
