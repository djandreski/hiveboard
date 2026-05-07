using System.Net;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Tests;

/// <summary>
/// Smoke-tests the in-process MCP server against the same WebApplicationFactory
/// used by REST integration tests. Uses the official MCP client over the test
/// HttpClient (no live socket required) — proves <c>list_tools</c>,
/// <c>list_resources</c>, a successful tool call, and a structured-error
/// invocation all flow through end-to-end.
/// </summary>
public class McpServerIntegrationTests
{
    private static readonly string[] ExpectedTools =
    {
        "hiveboard_list_tasks",
        "hiveboard_get_task",
        "hiveboard_update_status",
        "hiveboard_add_note",
        "hiveboard_decompose_task",
        "hiveboard_add_decision",
        "hiveboard_get_dependencies",
        "hiveboard_my_tasks",
        "hiveboard_get_notifications"
    };

    private static readonly string[] ExpectedResourceTemplates =
    {
        "hiveboard://project/{projectId}/overview",
        "hiveboard://task/{taskId}/context",
        "hiveboard://project/{projectId}/decisions"
    };

    [Fact]
    public async Task McpEndpoint_RejectsRequestsWithoutApiKey()
    {
        await using var app = new HiveboardApiFactory();
        using var anonymousClient = app.CreateAnonymousClient();

        var response = await anonymousClient.PostAsync(
            "/mcp",
            new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""",
                System.Text.Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListTools_ExposesAllNineHiveboardTools()
    {
        await using var app = new HiveboardApiFactory();
        await using var client = await CreateMcpClientAsync(app, app.AdminApiKey);

        var tools = await client.ListToolsAsync();

        var toolNames = tools.Select(tool => tool.Name).OrderBy(name => name).ToArray();
        Assert.Equal(ExpectedTools.OrderBy(name => name).ToArray(), toolNames);

        foreach (var tool in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description),
                $"Tool '{tool.Name}' must declare a description (PRD §5.7).");
            Assert.NotEqual(default, tool.JsonSchema);
        }
    }

    [Fact]
    public async Task ListResources_ExposesAllThreeHiveboardResourceTemplates()
    {
        await using var app = new HiveboardApiFactory();
        await using var client = await CreateMcpClientAsync(app, app.AdminApiKey);

        var resourceTemplates = await client.ListResourceTemplatesAsync();

        var uriTemplates = resourceTemplates.Select(template => template.UriTemplate).OrderBy(uri => uri).ToArray();
        Assert.Equal(ExpectedResourceTemplates.OrderBy(uri => uri).ToArray(), uriTemplates);

        foreach (var template in resourceTemplates)
        {
            Assert.False(string.IsNullOrWhiteSpace(template.Description),
                $"Resource '{template.UriTemplate}' must declare a description (PRD §5.7).");
        }
    }

    [Fact]
    public async Task CallToolHiveboardListTasks_ReturnsAssignedTasksForProject()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var project = IntegrationTestData.CreateProject(organization.Id, "MCP Project");
        var task = new AgentTask
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "MCP demo task",
            Description = "Verify MCP listing.",
            Status = TaskStatusEnum.Backlog,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Projects.Add(project);
            db.AgentTasks.Add(task);
        });

        await using var client = await CreateMcpClientAsync(app, app.AdminApiKey);

        var result = await client.CallToolAsync(
            "hiveboard_list_tasks",
            new Dictionary<string, object?>
            {
                ["projectId"] = project.Id.ToString()
            });

        Assert.NotEqual(true, result.IsError);

        var contentText = FormatErrorContent(result);
        Assert.Contains(task.Id.ToString(), contentText);
        Assert.Contains("MCP demo task", contentText);
    }

    [Fact]
    public async Task CallToolHiveboardGetTask_WithMalformedGuid_ReturnsStructuredErrorCode()
    {
        await using var app = new HiveboardApiFactory();
        await using var client = await CreateMcpClientAsync(app, app.AdminApiKey);

        var result = await client.CallToolAsync(
            "hiveboard_get_task",
            new Dictionary<string, object?>
            {
                ["taskId"] = "not-a-guid"
            });

        Assert.True(result.IsError, "Tool call with a malformed GUID should report IsError=true.");
        var content = FormatErrorContent(result);
        Assert.Contains("[invalid_argument]", content);
        Assert.Contains("taskId", content);
    }

    [Fact]
    public async Task CallToolHiveboardGetTask_WithUnknownGuid_ReturnsNotFoundCode()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        await app.SeedAsync(db => db.Organizations.Add(organization));

        await using var client = await CreateMcpClientAsync(app, app.AdminApiKey);

        var result = await client.CallToolAsync(
            "hiveboard_get_task",
            new Dictionary<string, object?>
            {
                ["taskId"] = Guid.NewGuid().ToString()
            });

        Assert.True(result.IsError, "Tool call with an unknown task ID should report IsError=true.");
        Assert.Contains("[not_found]", FormatErrorContent(result));
    }

    private static async Task<McpClient> CreateMcpClientAsync(HiveboardApiFactory app, string apiKey)
    {
        var httpClient = app.CreateAnonymousClient();
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                Name = "hiveboard-test"
            },
            httpClient,
            loggerFactory: null,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport);
    }

    private static string FormatErrorContent(CallToolResult result)
    {
        if (result.Content is null)
            return "<no content>";

        return string.Join(
            " | ",
            result.Content.Select(content => content switch
            {
                TextContentBlock text => text.Text,
                _ => content.GetType().Name
            }));
    }
}
