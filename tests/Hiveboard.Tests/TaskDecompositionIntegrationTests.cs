using System.Net;
using System.Net.Http.Json;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Tests;

public class TaskDecompositionIntegrationTests
{
    private const string WorkerAApiKey =
        "hb_sk_decompose_worker_a_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string WorkerBApiKey =
        "hb_sk_decompose_worker_b_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OrchestratorApiKey =
        "hb_sk_decompose_orchestrator_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task DecompositionEndpoint_CreatesSubtasks_TransitionsParent_AndNotifiesCoordinatorAndOrchestrator()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var worker = IntegrationTestData.CreateAgent(organization.Id, "Worker A", AgentType.Worker, WorkerAApiKey);
        var orchestrator = IntegrationTestData.CreateAgent(organization.Id, "Project Orchestrator", AgentType.Orchestrator, OrchestratorApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Decomposition Project");
        project.OrchestratorAgentId = orchestrator.Id;
        var epic = IntegrationTestData.CreateEpic(project.Id, "Core Epic");
        var parentTask = CreateTask(project.Id, epic.Id, "Parent Task", TaskStatusEnum.Assigned, worker.Id);

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.AddRange(worker, orchestrator);
            db.Projects.Add(project);
            db.Epics.Add(epic);
            db.AgentTasks.Add(parentTask);
        });

        using var workerClient = app.CreateAuthenticatedClient(WorkerAApiKey);

        var response = await workerClient.PostAsJsonAsync(
            $"/api/v1/tasks/{parentTask.Id}/subtasks",
            new
            {
                subtasks = new[]
                {
                    new { title = "Design endpoint", description = "Define the route and contract." },
                    new { title = "Write tests", description = "Cover workflow and validation." }
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var createdSubtasks = await response.Content.ReadFromJsonAsync<List<TaskResponse>>();
        Assert.NotNull(createdSubtasks);
        Assert.Collection(
            createdSubtasks!,
            subtask =>
            {
                Assert.NotEqual(Guid.Empty, subtask.Id);
                Assert.Equal("Design endpoint", subtask.Title);
                Assert.Equal("backlog", subtask.Status);
                Assert.Null(subtask.AssignedAgentId);
                Assert.Equal(epic.Id, subtask.EpicId);
            },
            subtask =>
            {
                Assert.NotEqual(Guid.Empty, subtask.Id);
                Assert.Equal("Write tests", subtask.Title);
                Assert.Equal("backlog", subtask.Status);
                Assert.Null(subtask.AssignedAgentId);
                Assert.Equal(epic.Id, subtask.EpicId);
            });

        var artifacts = await app.QueryAsync(async db =>
        {
            var persistedParent = await db.AgentTasks
                .SingleAsync(candidate => candidate.Id == parentTask.Id);

            var persistedSubtasks = await db.AgentTasks
                .Where(candidate => candidate.ParentTaskId == parentTask.Id)
                .OrderBy(candidate => candidate.Title)
                .Select(candidate => new
                {
                    candidate.ProjectId,
                    candidate.EpicId,
                    candidate.ParentTaskId,
                    candidate.Status
                })
                .ToListAsync();

            var decompositionEventExists = await db.TaskEvents
                .AnyAsync(taskEvent =>
                    taskEvent.TaskId == parentTask.Id &&
                    taskEvent.EventType == "decomposed" &&
                    taskEvent.NewValue == "2");

            var parentStatusChangedEventExists = await db.TaskEvents
                .AnyAsync(taskEvent =>
                    taskEvent.TaskId == parentTask.Id &&
                    taskEvent.EventType == "status_changed" &&
                    taskEvent.OldValue == "assigned" &&
                    taskEvent.NewValue == "inprogress");

            var coordinatorNotificationExists = await db.Notifications
                .Include(notification => notification.Agent)
                .AnyAsync(notification =>
                    notification.TaskId == parentTask.Id &&
                    notification.Type == NotificationType.TaskDecomposed &&
                    notification.Message == "Task 'Parent Task' was decomposed into 2 subtasks by Worker A" &&
                    notification.Agent != null &&
                    notification.Agent.Name == "Coordinator");

            var orchestratorNotificationExists = await db.Notifications
                .AnyAsync(notification =>
                    notification.TaskId == parentTask.Id &&
                    notification.AgentId == orchestrator.Id &&
                    notification.Type == NotificationType.TaskDecomposed &&
                    notification.Message == "Task 'Parent Task' was decomposed into 2 subtasks by Worker A");

            return new
            {
                persistedParent.Status,
                persistedSubtasks,
                decompositionEventExists,
                parentStatusChangedEventExists,
                coordinatorNotificationExists,
                orchestratorNotificationExists
            };
        });

        Assert.Equal(TaskStatusEnum.InProgress, artifacts.Status);
        Assert.Equal(2, artifacts.persistedSubtasks.Count);
        Assert.All(artifacts.persistedSubtasks, subtask =>
        {
            Assert.Equal(project.Id, subtask.ProjectId);
            Assert.Equal(epic.Id, subtask.EpicId);
            Assert.Equal(parentTask.Id, subtask.ParentTaskId);
            Assert.Equal(TaskStatusEnum.Backlog, subtask.Status);
        });
        Assert.True(artifacts.decompositionEventExists);
        Assert.True(artifacts.parentStatusChangedEventExists);
        Assert.True(artifacts.coordinatorNotificationExists);
        Assert.True(artifacts.orchestratorNotificationExists);
    }

    [Fact]
    public async Task DecompositionEndpoint_RejectsOtherWorkers_ValidatesPayload_AndAllowsConfiguredOrchestrator()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var workerA = IntegrationTestData.CreateAgent(organization.Id, "Worker A", AgentType.Worker, WorkerAApiKey);
        var workerB = IntegrationTestData.CreateAgent(organization.Id, "Worker B", AgentType.Worker, WorkerBApiKey);
        var orchestrator = IntegrationTestData.CreateAgent(organization.Id, "Project Orchestrator", AgentType.Orchestrator, OrchestratorApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Authorization Project");
        project.OrchestratorAgentId = orchestrator.Id;
        var parentTask = CreateTask(project.Id, null, "Parent Task", TaskStatusEnum.Assigned, workerA.Id);

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.AddRange(workerA, workerB, orchestrator);
            db.Projects.Add(project);
            db.AgentTasks.Add(parentTask);
        });

        using var workerAClient = app.CreateAuthenticatedClient(WorkerAApiKey);
        using var workerBClient = app.CreateAuthenticatedClient(WorkerBApiKey);
        using var orchestratorClient = app.CreateAuthenticatedClient(OrchestratorApiKey);

        var forbiddenResponse = await workerBClient.PostAsJsonAsync(
            $"/api/v1/tasks/{parentTask.Id}/subtasks",
            new
            {
                subtasks = new[]
                {
                    new { title = "Should fail", description = "Wrong worker" }
                }
            });

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        var emptyResponse = await workerAClient.PostAsJsonAsync(
            $"/api/v1/tasks/{parentTask.Id}/subtasks",
            new { subtasks = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);

        var tooManyResponse = await workerAClient.PostAsJsonAsync(
            $"/api/v1/tasks/{parentTask.Id}/subtasks",
            new
            {
                subtasks = Enumerable.Range(1, 51)
                    .Select(index => new { title = $"Subtask {index}", description = string.Empty })
                    .ToArray()
            });

        Assert.Equal(HttpStatusCode.BadRequest, tooManyResponse.StatusCode);

        var blankTitleResponse = await workerAClient.PostAsJsonAsync(
            $"/api/v1/tasks/{parentTask.Id}/subtasks",
            new
            {
                subtasks = new[]
                {
                    new { title = " ", description = "Missing title" }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, blankTitleResponse.StatusCode);

        var orchestratorResponse = await orchestratorClient.PostAsJsonAsync(
            $"/api/v1/tasks/{parentTask.Id}/subtasks",
            new
            {
                subtasks = new[]
                {
                    new { title = "Configured orchestrator subtask", description = "Allowed actor" }
                }
            });

        Assert.Equal(HttpStatusCode.OK, orchestratorResponse.StatusCode);

        var createdSubtasks = await orchestratorResponse.Content.ReadFromJsonAsync<List<TaskResponse>>();
        Assert.NotNull(createdSubtasks);
        Assert.Single(createdSubtasks!);
        Assert.Equal("Configured orchestrator subtask", createdSubtasks[0].Title);
        Assert.Equal("backlog", createdSubtasks[0].Status);

        var artifacts = await app.QueryAsync(async db =>
        {
            var persistedParent = await db.AgentTasks
                .SingleAsync(candidate => candidate.Id == parentTask.Id);

            var subtaskCount = await db.AgentTasks
                .CountAsync(candidate => candidate.ParentTaskId == parentTask.Id);

            return new
            {
                persistedParent.Status,
                subtaskCount
            };
        });

        Assert.Equal(TaskStatusEnum.InProgress, artifacts.Status);
        Assert.Equal(1, artifacts.subtaskCount);
    }

    private static AgentTask CreateTask(
        Guid projectId,
        Guid? epicId,
        string title,
        TaskStatusEnum status,
        Guid? assignedAgentId = null,
        Guid? parentTaskId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            EpicId = epicId,
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
