using System.Net;
using System.Net.Http.Json;
using Hiveboard.Api.Contracts;
using Hiveboard.Tests.Infrastructure;

namespace Hiveboard.Tests;

public class AgentLifecycleIntegrationTests
{
    [Fact]
    public async Task RegisterDeactivateAndRotateAgentKey_EnforcesLifecycleRules()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateOrganization("Lifecycle Org");
        await app.SeedAsync(db => db.Organizations.Add(organization));

        using var adminClient = app.CreateAuthenticatedClient(app.AdminApiKey);

        var registerResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/agents/register",
            new RegisterAgentRequest("Lifecycle Worker", "worker", "codex", organization.Id));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registeredAgent = await registerResponse.Content.ReadFromJsonAsync<RegisterAgentResponse>();
        Assert.NotNull(registeredAgent);
        Assert.False(string.IsNullOrWhiteSpace(registeredAgent.ApiKey));

        using var initialAgentClient = app.CreateAuthenticatedClient(registeredAgent.ApiKey);
        var meBeforeDeactivate = await initialAgentClient.GetAsync("/api/v1/agents/me");
        Assert.Equal(HttpStatusCode.OK, meBeforeDeactivate.StatusCode);

        var deactivateResponse = await adminClient.DeleteAsync($"/api/v1/agents/{registeredAgent.AgentId}");
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var oldKeyAfterDeactivate = await initialAgentClient.GetAsync("/api/v1/agents/me");
        Assert.Equal(HttpStatusCode.Unauthorized, oldKeyAfterDeactivate.StatusCode);

        var invalidReactivationRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/agents/{registeredAgent.AgentId}")
        {
            Content = JsonContent.Create(new { status = "active" })
        };
        var invalidReactivationResponse = await adminClient.SendAsync(invalidReactivationRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalidReactivationResponse.StatusCode);

        var rotateResponse = await adminClient.PostAsync(
            $"/api/v1/agents/{registeredAgent.AgentId}/keys/rotate",
            content: null);
        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);

        var keyRotation = await rotateResponse.Content.ReadFromJsonAsync<KeyRotationResponse>();
        Assert.NotNull(keyRotation);
        Assert.False(string.IsNullOrWhiteSpace(keyRotation.ApiKey));

        var oldKeyAfterRotation = await initialAgentClient.GetAsync("/api/v1/agents/me");
        Assert.Equal(HttpStatusCode.Unauthorized, oldKeyAfterRotation.StatusCode);

        using var rotatedAgentClient = app.CreateAuthenticatedClient(keyRotation.ApiKey);
        var meAfterRotation = await rotatedAgentClient.GetAsync("/api/v1/agents/me");
        Assert.Equal(HttpStatusCode.OK, meAfterRotation.StatusCode);

        var mePayload = await meAfterRotation.Content.ReadFromJsonAsync<AgentMeResponse>();
        Assert.NotNull(mePayload);
        Assert.Equal(registeredAgent.AgentId, mePayload.Id);
        Assert.Equal("active", mePayload.Status);
    }

    private sealed record AgentMeResponse(Guid Id, string Name, string Type, string Platform, string Status, Guid OrganizationId);
}
