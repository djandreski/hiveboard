using System.Net;
using System.Net.Http.Json;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Tests;

public class ProjectEpicCrudIntegrationTests
{
    private const string WorkerApiKey =
        "hb_sk_project_worker_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task CoordinatorFirstProjectAndEpicEndpoints_RespectRoleRules_AndSupportCreateAndReadFlows()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var worker = IntegrationTestData.CreateAgent(
            organization.Id,
            "Project Worker",
            AgentType.Worker,
            WorkerApiKey);

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.Add(worker);
        });

        using var coordinatorClient = app.CreateCoordinatorClient();
        using var workerClient = app.CreateAuthenticatedClient(WorkerApiKey);

        var createProjectResponse = await coordinatorClient.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectRequest("Platform Upgrade", "Upgrade to latest stack"));
        Assert.Equal(HttpStatusCode.Created, createProjectResponse.StatusCode);

        var createdProject = await createProjectResponse.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(createdProject);
        Assert.Equal("Platform Upgrade", createdProject.Name);

        var projectOrchestratorId = await app.QueryAsync(db => db.Projects
            .Where(project => project.Id == createdProject.Id)
            .Select(project => EF.Property<Guid?>(project, "OrchestratorAgentId"))
            .SingleAsync());
        Assert.Null(projectOrchestratorId);

        var workerCreateProjectResponse = await workerClient.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectRequest("Forbidden Project", "Should fail"));
        Assert.Equal(HttpStatusCode.Forbidden, workerCreateProjectResponse.StatusCode);

        var workerProjectList = await workerClient.GetFromJsonAsync<List<ProjectResponse>>("/api/v1/projects");
        Assert.NotNull(workerProjectList);
        Assert.Single(workerProjectList);
        Assert.Equal(createdProject.Id, workerProjectList[0].Id);

        var workerProjectDetails = await workerClient.GetAsync($"/api/v1/projects/{createdProject.Id}");
        Assert.Equal(HttpStatusCode.OK, workerProjectDetails.StatusCode);

        var createEpicResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/projects/{createdProject.Id}/epics",
            new CreateEpicRequest("Authentication Hardening", "Improve auth safeguards"));
        Assert.Equal(HttpStatusCode.Created, createEpicResponse.StatusCode);

        var createdEpic = await createEpicResponse.Content.ReadFromJsonAsync<EpicResponse>();
        Assert.NotNull(createdEpic);
        Assert.Equal("Authentication Hardening", createdEpic.Title);

        var workerCreateEpicResponse = await workerClient.PostAsJsonAsync(
            $"/api/v1/projects/{createdProject.Id}/epics",
            new CreateEpicRequest("Forbidden Epic", "Should fail"));
        Assert.Equal(HttpStatusCode.Forbidden, workerCreateEpicResponse.StatusCode);

        var workerEpicList = await workerClient.GetFromJsonAsync<List<EpicResponse>>(
            $"/api/v1/projects/{createdProject.Id}/epics");
        Assert.NotNull(workerEpicList);
        Assert.Single(workerEpicList);
        Assert.Equal(createdEpic.Id, workerEpicList[0].Id);

        var workerEpicDetailsResponse = await workerClient.GetAsync($"/api/v1/epics/{createdEpic.Id}");
        Assert.Equal(HttpStatusCode.OK, workerEpicDetailsResponse.StatusCode);

        var workerEpicDetails = await workerEpicDetailsResponse.Content.ReadFromJsonAsync<EpicResponse>();
        Assert.NotNull(workerEpicDetails);
        Assert.Equal(createdEpic.Id, workerEpicDetails.Id);
        Assert.NotNull(workerEpicDetails.Tasks);
        Assert.Empty(workerEpicDetails.Tasks);
    }
}
