using System.Net;
using System.Net.Http.Json;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;

namespace Hiveboard.Tests;

public class TenantScopingIntegrationTests
{
    private const string OrgAOrchestratorApiKey =
        "hb_sk_orga_orchestrator_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OrgAWorkerApiKey =
        "hb_sk_orga_worker_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OrgBOrchestratorApiKey =
        "hb_sk_orgb_orchestrator_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task AgentAccess_IsRestrictedToItsOrganization()
    {
        await using var app = new HiveboardApiFactory();

        var orgA = IntegrationTestData.CreateOrganization("Org A");
        var orgB = IntegrationTestData.CreateOrganization("Org B");

        var orgAOrchestrator = IntegrationTestData.CreateAgent(
            orgA.Id,
            "Org A Orchestrator",
            AgentType.Orchestrator,
            OrgAOrchestratorApiKey);
        var orgAWorker = IntegrationTestData.CreateAgent(
            orgA.Id,
            "Org A Worker",
            AgentType.Worker,
            OrgAWorkerApiKey);
        var orgBOrchestrator = IntegrationTestData.CreateAgent(
            orgB.Id,
            "Org B Orchestrator",
            AgentType.Orchestrator,
            OrgBOrchestratorApiKey);

        var orgAProject = IntegrationTestData.CreateProject(orgA.Id, "Org A Project");
        var orgBProject = IntegrationTestData.CreateProject(orgB.Id, "Org B Project");
        var orgAEpic = IntegrationTestData.CreateEpic(orgAProject.Id, "Org A Epic");
        var orgBEpic = IntegrationTestData.CreateEpic(orgBProject.Id, "Org B Epic");

        await app.SeedAsync(db =>
        {
            db.Organizations.AddRange(orgA, orgB);
            db.Agents.AddRange(orgAOrchestrator, orgAWorker, orgBOrchestrator);
            db.Projects.AddRange(orgAProject, orgBProject);
            db.Epics.AddRange(orgAEpic, orgBEpic);
        });

        using var orgAClient = app.CreateAuthenticatedClient(OrgAOrchestratorApiKey);

        var agents = await orgAClient.GetFromJsonAsync<List<AgentSummary>>("/api/v1/agents");
        var projects = await orgAClient.GetFromJsonAsync<List<ProjectResponse>>("/api/v1/projects");
        var epics = await orgAClient.GetFromJsonAsync<List<EpicResponse>>($"/api/v1/projects/{orgAProject.Id}/epics");

        var foreignProjectResponse = await orgAClient.GetAsync($"/api/v1/projects/{orgBProject.Id}");
        var foreignProjectEpicsResponse = await orgAClient.GetAsync($"/api/v1/projects/{orgBProject.Id}/epics");
        var foreignEpicResponse = await orgAClient.GetAsync($"/api/v1/epics/{orgBEpic.Id}");

        Assert.NotNull(agents);
        Assert.Equal(2, agents.Count);
        Assert.DoesNotContain(agents, agent => agent.Name == orgBOrchestrator.Name);

        Assert.NotNull(projects);
        Assert.Single(projects);
        Assert.Equal(orgAProject.Id, projects[0].Id);

        Assert.NotNull(epics);
        Assert.Single(epics);
        Assert.Equal(orgAEpic.Id, epics[0].Id);

        Assert.Equal(HttpStatusCode.Forbidden, foreignProjectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreignProjectEpicsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreignEpicResponse.StatusCode);
    }

    [Fact]
    public async Task AdminKey_IsRejectedByOrganizationScopedProjectAndEpicEndpoints()
    {
        await using var app = new HiveboardApiFactory();
        using var adminClient = app.CreateAuthenticatedClient(app.AdminApiKey);

        var projectsResponse = await adminClient.GetAsync("/api/v1/projects");
        var createProjectResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectRequest("Admin Project", "Should not be allowed"));
        var projectEpicsResponse = await adminClient.GetAsync($"/api/v1/projects/{Guid.NewGuid()}/epics");
        var epicDetailsResponse = await adminClient.GetAsync($"/api/v1/epics/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, projectsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createProjectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, projectEpicsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, epicDetailsResponse.StatusCode);

        var error = await projectsResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("Organization-scoped endpoints require an agent API key", error.error);
    }

    private sealed record AgentSummary(Guid Id, string Name, string Type, string Platform, string Status);
    private sealed record ErrorResponse(string error);
}
