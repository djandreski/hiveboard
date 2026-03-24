using System.Net;
using System.Net.Http.Json;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;

namespace Hiveboard.Tests;

public class AuthIntegrationTests
{
    private const string OrchestratorApiKey =
        "hb_sk_auth_orchestrator_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task HealthEndpoint_AllowsAnonymousRequests()
    {
        await using var app = new HiveboardApiFactory();
        using var client = app.CreateAnonymousClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_RejectMissingAndInvalidKeys()
    {
        await using var app = new HiveboardApiFactory();
        using var anonymousClient = app.CreateAnonymousClient();
        using var invalidKeyClient = app.CreateAuthenticatedClient("hb_sk_invalid");

        var missingHeaderResponse = await anonymousClient.GetAsync("/api/v1/agents");
        var invalidHeaderResponse = await invalidKeyClient.GetAsync("/api/v1/agents");

        Assert.Equal(HttpStatusCode.Unauthorized, missingHeaderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidHeaderResponse.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoints_RequireAdminKey()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateOrganization("Auth Org");
        var orchestrator = IntegrationTestData.CreateAgent(
            organization.Id,
            "Auth Orchestrator",
            AgentType.Orchestrator,
            OrchestratorApiKey);

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.Add(orchestrator);
        });

        using var agentClient = app.CreateAuthenticatedClient(OrchestratorApiKey);
        using var adminClient = app.CreateAuthenticatedClient(app.AdminApiKey);

        var forbiddenResponse = await agentClient.GetAsync("/api/v1/admin/keys/info");
        var successResponse = await adminClient.GetAsync("/api/v1/admin/keys/info");
        var payload = await successResponse.Content.ReadFromJsonAsync<AdminKeyInfoResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(app.AdminApiKey[..12], payload.Prefix);
    }
}
