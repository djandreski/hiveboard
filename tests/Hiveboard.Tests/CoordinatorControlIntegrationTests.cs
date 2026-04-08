using System.Net;
using System.Net.Http.Json;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Tests;

public class CoordinatorControlIntegrationTests
{
    private const string WorkerApiKey =
        "hb_sk_coordinator_worker_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OrchestratorApiKey =
        "hb_sk_coordinator_orchestrator_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task CoordinatorCredential_CanUseResolvedOrganization_ForCrudAndAssignment()
    {
        await using var app = new HiveboardApiFactory();

        var defaultOrganization = IntegrationTestData.CreateDefaultOrganization();
        var foreignOrganization = IntegrationTestData.CreateOrganization("Foreign Org");
        var worker = IntegrationTestData.CreateAgent(
            defaultOrganization.Id,
            "Coordinator Worker",
            AgentType.Worker,
            WorkerApiKey);
        var foreignProject = IntegrationTestData.CreateProject(foreignOrganization.Id, "Foreign Project");

        await app.SeedAsync(db =>
        {
            db.Organizations.AddRange(defaultOrganization, foreignOrganization);
            db.Agents.Add(worker);
            db.Projects.Add(foreignProject);
        });

        using var coordinatorClient = app.CreateCoordinatorClient();

        var createProjectResponse = await coordinatorClient.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectRequest("Coordinator Project", "Created by the coordinator credential"));
        Assert.Equal(HttpStatusCode.Created, createProjectResponse.StatusCode);

        var createdProject = await createProjectResponse.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(createdProject);

        var listProjects = await coordinatorClient.GetFromJsonAsync<List<ProjectResponse>>("/api/v1/projects");
        Assert.NotNull(listProjects);
        Assert.Single(listProjects);
        Assert.Equal(createdProject.Id, listProjects[0].Id);

        var createEpicResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/projects/{createdProject.Id}/epics",
            new CreateEpicRequest("Coordinator Epic", "Scoped to the resolved organization"));
        Assert.Equal(HttpStatusCode.Created, createEpicResponse.StatusCode);

        var createdEpic = await createEpicResponse.Content.ReadFromJsonAsync<EpicResponse>();
        Assert.NotNull(createdEpic);

        var epicList = await coordinatorClient.GetFromJsonAsync<List<EpicResponse>>(
            $"/api/v1/projects/{createdProject.Id}/epics");
        Assert.NotNull(epicList);
        Assert.Single(epicList);
        Assert.Equal(createdEpic.Id, epicList[0].Id);

        var createTaskResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/projects/{createdProject.Id}/tasks",
            new CreateTaskRequest(
                "Coordinator Task",
                "Created and assigned by the coordinator credential",
                createdEpic.Id,
                null,
                new Dictionary<string, string> { ["branch"] = "feature/coordinator-flow" }));
        Assert.Equal(HttpStatusCode.Created, createTaskResponse.StatusCode);

        var createdTask = await createTaskResponse.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(createdTask);

        var updateTaskResponse = await coordinatorClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{createdTask.Id}",
            new UpdateTaskRequest(
                "Coordinator Task Updated",
                "Updated by the coordinator credential",
                createdEpic.Id,
                worker.Id,
                new Dictionary<string, string> { ["branch"] = "feature/coordinator-flow-updated" }));
        Assert.Equal(HttpStatusCode.OK, updateTaskResponse.StatusCode);

        var updatedTask = await updateTaskResponse.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(updatedTask);
        Assert.Equal("assigned", updatedTask!.Status);
        Assert.Equal(worker.Id, updatedTask.AssignedAgentId);

        var taskList = await coordinatorClient.GetFromJsonAsync<List<TaskResponse>>(
            $"/api/v1/projects/{createdProject.Id}/tasks?status=Assigned&agentId={worker.Id}&epicId={createdEpic.Id}");
        Assert.NotNull(taskList);
        Assert.Single(taskList);
        Assert.Equal(createdTask.Id, taskList[0].Id);

        var taskDetailResponse = await coordinatorClient.GetAsync($"/api/v1/tasks/{createdTask.Id}");
        Assert.Equal(HttpStatusCode.OK, taskDetailResponse.StatusCode);

        var taskDetail = await taskDetailResponse.Content.ReadFromJsonAsync<TaskDetailResponse>();
        Assert.NotNull(taskDetail);
        Assert.Equal("Coordinator", taskDetail!.Events.Single(taskEvent => taskEvent.EventType == "assigned").Agent);

        var projectOrchestratorId = await app.QueryAsync(db => db.Projects
            .Where(project => project.Id == createdProject.Id)
            .Select(project => EF.Property<Guid?>(project, "OrchestratorAgentId"))
            .SingleAsync());
        Assert.Null(projectOrchestratorId);
    }

    [Fact]
    public async Task WorkerRestrictionsRemainAndOrchestratorProjectAttachmentStillWorks()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var worker = IntegrationTestData.CreateAgent(
            organization.Id,
            "Worker",
            AgentType.Worker,
            WorkerApiKey);
        var orchestrator = IntegrationTestData.CreateAgent(
            organization.Id,
            "Optional Orchestrator",
            AgentType.Orchestrator,
            OrchestratorApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Seeded Project");
        var task = new AgentTask
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Existing Task",
            Description = "Used for worker authorization checks",
            Status = TaskStatusEnum.Backlog,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.AddRange(worker, orchestrator);
            db.Projects.Add(project);
            db.AgentTasks.Add(task);
        });

        using var workerClient = app.CreateAuthenticatedClient(WorkerApiKey);
        using var orchestratorClient = app.CreateAuthenticatedClient(OrchestratorApiKey);

        var workerCreateProjectResponse = await workerClient.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectRequest("Forbidden Project", "Workers cannot create projects"));
        var workerCreateEpicResponse = await workerClient.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/epics",
            new CreateEpicRequest("Forbidden Epic", "Workers cannot create epics"));
        var workerCreateTaskResponse = await workerClient.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/tasks",
            new CreateTaskRequest("Forbidden Task", "Workers cannot create tasks", null, null, null));
        var workerUpdateTaskResponse = await workerClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{task.Id}",
            new UpdateTaskRequest("Forbidden Update", null, null, worker.Id, null));

        Assert.Equal(HttpStatusCode.Forbidden, workerCreateProjectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, workerCreateEpicResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, workerCreateTaskResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, workerUpdateTaskResponse.StatusCode);

        var orchestratorCreateProjectResponse = await orchestratorClient.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectRequest("Orchestrator Project", "Created by optional orchestrator"));
        Assert.Equal(HttpStatusCode.Created, orchestratorCreateProjectResponse.StatusCode);

        var createdProject = await orchestratorCreateProjectResponse.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(createdProject);

        var orchestratorAgentId = await app.QueryAsync(db => db.Projects
            .Where(candidate => candidate.Id == createdProject.Id)
            .Select(candidate => EF.Property<Guid?>(candidate, "OrchestratorAgentId"))
            .SingleAsync());
        Assert.Equal(orchestrator.Id, orchestratorAgentId);
    }
}
