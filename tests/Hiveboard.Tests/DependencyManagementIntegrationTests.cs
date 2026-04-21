using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Tests;

public class DependencyManagementIntegrationTests
{
    private const string WorkerApiKey =
        "hb_sk_dependency_worker_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ConfiguredOrchestratorApiKey =
        "hb_sk_dependency_orchestrator_configured_aaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string UnconfiguredOrchestratorApiKey =
        "hb_sk_dependency_orchestrator_unconfigured_aaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task DependencyEndpoints_CreateRemoveAndGraphFlows_WorkForCoordinatorAndConfiguredOrchestrator()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var worker = IntegrationTestData.CreateAgent(organization.Id, "Worker", AgentType.Worker, WorkerApiKey);
        var orchestrator = IntegrationTestData.CreateAgent(
            organization.Id,
            "Configured Orchestrator",
            AgentType.Orchestrator,
            ConfiguredOrchestratorApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Dependency Project");
        project.OrchestratorAgentId = orchestrator.Id;

        var prerequisiteTask = CreateTask(project.Id, "Prerequisite Task", TaskStatusEnum.Done);
        var dependentTask = CreateTask(project.Id, "Dependent Task", TaskStatusEnum.Assigned, worker.Id);
        var backlogTask = CreateTask(project.Id, "Backlog Task", TaskStatusEnum.Backlog);

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.AddRange(worker, orchestrator);
            db.Projects.Add(project);
            db.AgentTasks.AddRange(prerequisiteTask, dependentTask, backlogTask);
        });

        using var coordinatorClient = app.CreateCoordinatorClient();
        using var workerClient = app.CreateAuthenticatedClient(WorkerApiKey);
        using var orchestratorClient = app.CreateAuthenticatedClient(ConfiguredOrchestratorApiKey);

        var createResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/tasks/{dependentTask.Id}/dependencies",
            new CreateDependencyRequest(prerequisiteTask.Id));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdDependency = await createResponse.Content.ReadFromJsonAsync<TaskDependencyResponse>();
        Assert.NotNull(createdDependency);
        Assert.Equal(dependentTask.Id, createdDependency!.TaskId);
        Assert.Equal(prerequisiteTask.Id, createdDependency.DependsOnTaskId);
        Assert.Equal("blocks", createdDependency.Type);

        var graph = await workerClient.GetFromJsonAsync<DependencyGraphResponse>(
            $"/api/v1/projects/{project.Id}/dependencies/graph");

        Assert.NotNull(graph);
        Assert.Equal(3, graph!.Nodes.Count);
        Assert.Contains(graph.Nodes, node =>
            node.TaskId == dependentTask.Id &&
            node.Title == dependentTask.Title &&
            node.Status == nameof(TaskStatusEnum.Assigned) &&
            node.AssignedAgentId == worker.Id);
        Assert.Contains(graph.Edges, edge =>
            edge.From == dependentTask.Id &&
            edge.To == prerequisiteTask.Id &&
            edge.Type == "blocks" &&
            edge.DepId == createdDependency.Id);

        var removeResponse = await coordinatorClient.DeleteAsync(
            $"/api/v1/tasks/{dependentTask.Id}/dependencies/{createdDependency.Id}");

        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        var removeArtifacts = await app.QueryAsync(async db => new
        {
            DependencyCount = await db.TaskDependencies.CountAsync(),
            UpdatedTaskTimestamp = await db.AgentTasks
                .Where(candidate => candidate.Id == dependentTask.Id)
                .Select(candidate => candidate.UpdatedAt)
                .SingleAsync()
        });

        Assert.Equal(0, removeArtifacts.DependencyCount);
        Assert.True(removeArtifacts.UpdatedTaskTimestamp >= dependentTask.UpdatedAt);

        var orchestratorCreateResponse = await orchestratorClient.PostAsJsonAsync(
            $"/api/v1/tasks/{backlogTask.Id}/dependencies",
            new CreateDependencyRequest(prerequisiteTask.Id));

        Assert.Equal(HttpStatusCode.Created, orchestratorCreateResponse.StatusCode);

        var orchestratorDependency = await orchestratorCreateResponse.Content.ReadFromJsonAsync<TaskDependencyResponse>();
        Assert.NotNull(orchestratorDependency);

        var finalGraph = await workerClient.GetFromJsonAsync<DependencyGraphResponse>(
            $"/api/v1/projects/{project.Id}/dependencies/graph");

        Assert.NotNull(finalGraph);
        Assert.Contains(finalGraph!.Edges, edge =>
            edge.From == backlogTask.Id &&
            edge.To == prerequisiteTask.Id &&
            edge.Type == "blocks" &&
            edge.DepId == orchestratorDependency!.Id);

        var orchestratorRemoveResponse = await orchestratorClient.DeleteAsync(
            $"/api/v1/tasks/{backlogTask.Id}/dependencies/{orchestratorDependency!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, orchestratorRemoveResponse.StatusCode);

        var finalDependencyCount = await app.QueryAsync(db => db.TaskDependencies.CountAsync());
        Assert.Equal(0, finalDependencyCount);
    }

    [Fact]
    public async Task DependencyEndpoints_RejectSelfCrossProjectMissingAndCircularDependencies()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var project = IntegrationTestData.CreateProject(organization.Id, "Primary Project");
        var secondaryProject = IntegrationTestData.CreateProject(organization.Id, "Secondary Project");

        var taskA = CreateTask(project.Id, "Task A", TaskStatusEnum.Backlog);
        var taskB = CreateTask(project.Id, "Task B", TaskStatusEnum.Backlog);
        var taskC = CreateTask(project.Id, "Task C", TaskStatusEnum.Backlog);
        var foreignTask = CreateTask(secondaryProject.Id, "Foreign Task", TaskStatusEnum.Backlog);

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Projects.AddRange(project, secondaryProject);
            db.AgentTasks.AddRange(taskA, taskB, taskC, foreignTask);
            db.TaskDependencies.AddRange(
                new TaskDependency
                {
                    Id = Guid.NewGuid(),
                    TaskId = taskB.Id,
                    DependsOnTaskId = taskC.Id,
                    Type = DependencyType.Blocks
                },
                new TaskDependency
                {
                    Id = Guid.NewGuid(),
                    TaskId = taskC.Id,
                    DependsOnTaskId = taskA.Id,
                    Type = DependencyType.Blocks
                });
        });

        using var coordinatorClient = app.CreateCoordinatorClient();

        var circularResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/tasks/{taskA.Id}/dependencies",
            new CreateDependencyRequest(taskB.Id));
        Assert.Equal(HttpStatusCode.BadRequest, circularResponse.StatusCode);

        using (var circularPayload = JsonDocument.Parse(await circularResponse.Content.ReadAsStringAsync()))
        {
            var error = circularPayload.RootElement.GetProperty("error").GetString();
            Assert.NotNull(error);
            Assert.Contains("Circular dependency detected", error);
            Assert.Contains("Task A -> Task B -> Task C -> Task A", error);

            var cyclePath = circularPayload.RootElement.GetProperty("cyclePath");
            Assert.Equal(4, cyclePath.GetArrayLength());
        }

        var selfResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/tasks/{taskA.Id}/dependencies",
            new CreateDependencyRequest(taskA.Id));
        Assert.Equal(HttpStatusCode.BadRequest, selfResponse.StatusCode);

        using (var selfPayload = JsonDocument.Parse(await selfResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("A task cannot depend on itself.", selfPayload.RootElement.GetProperty("error").GetString());
        }

        var crossProjectResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/tasks/{taskA.Id}/dependencies",
            new CreateDependencyRequest(foreignTask.Id));
        Assert.Equal(HttpStatusCode.BadRequest, crossProjectResponse.StatusCode);

        using (var crossProjectPayload = JsonDocument.Parse(await crossProjectResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                "Dependencies can only be created between tasks in the same project.",
                crossProjectPayload.RootElement.GetProperty("error").GetString());
        }

        var missingDependencyResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/tasks/{taskA.Id}/dependencies",
            new CreateDependencyRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, missingDependencyResponse.StatusCode);

        using (var missingDependencyPayload = JsonDocument.Parse(await missingDependencyResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Dependency task not found.", missingDependencyPayload.RootElement.GetProperty("error").GetString());
        }
    }

    [Fact]
    public async Task DependencyEndpoints_RejectWorkerAndUnconfiguredOrchestratorMutations()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var worker = IntegrationTestData.CreateAgent(organization.Id, "Worker", AgentType.Worker, WorkerApiKey);
        var unconfiguredOrchestrator = IntegrationTestData.CreateAgent(
            organization.Id,
            "Unconfigured Orchestrator",
            AgentType.Orchestrator,
            UnconfiguredOrchestratorApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Authorization Project");

        var prerequisiteTask = CreateTask(project.Id, "Prerequisite Task", TaskStatusEnum.Done);
        var dependentTask = CreateTask(project.Id, "Dependent Task", TaskStatusEnum.Assigned, worker.Id);
        var existingDependency = new TaskDependency
        {
            Id = Guid.NewGuid(),
            TaskId = dependentTask.Id,
            DependsOnTaskId = prerequisiteTask.Id,
            Type = DependencyType.Blocks
        };

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.AddRange(worker, unconfiguredOrchestrator);
            db.Projects.Add(project);
            db.AgentTasks.AddRange(prerequisiteTask, dependentTask);
            db.TaskDependencies.Add(existingDependency);
        });

        using var workerClient = app.CreateAuthenticatedClient(WorkerApiKey);
        using var orchestratorClient = app.CreateAuthenticatedClient(UnconfiguredOrchestratorApiKey);

        var workerCreateResponse = await workerClient.PostAsJsonAsync(
            $"/api/v1/tasks/{dependentTask.Id}/dependencies",
            new CreateDependencyRequest(prerequisiteTask.Id));
        Assert.Equal(HttpStatusCode.Forbidden, workerCreateResponse.StatusCode);

        var orchestratorCreateResponse = await orchestratorClient.PostAsJsonAsync(
            $"/api/v1/tasks/{dependentTask.Id}/dependencies",
            new CreateDependencyRequest(prerequisiteTask.Id));
        Assert.Equal(HttpStatusCode.Forbidden, orchestratorCreateResponse.StatusCode);

        var workerDeleteResponse = await workerClient.DeleteAsync(
            $"/api/v1/tasks/{dependentTask.Id}/dependencies/{existingDependency.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, workerDeleteResponse.StatusCode);

        var orchestratorDeleteResponse = await orchestratorClient.DeleteAsync(
            $"/api/v1/tasks/{dependentTask.Id}/dependencies/{existingDependency.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, orchestratorDeleteResponse.StatusCode);

        var dependencyCount = await app.QueryAsync(db => db.TaskDependencies.CountAsync());
        Assert.Equal(1, dependencyCount);
    }

    private static AgentTask CreateTask(
        Guid projectId,
        string title,
        TaskStatusEnum status,
        Guid? assignedAgentId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AssignedAgentId = assignedAgentId,
            Title = title,
            Description = string.Empty,
            Status = status,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}
