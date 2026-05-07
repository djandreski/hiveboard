using ModelContextProtocol.Protocol;

namespace Hiveboard.Api.Mcp;

/// <summary>
/// Registers the Hiveboard MCP (Model Context Protocol) server.
///
/// The server is hosted in-process alongside the REST API and exposes the
/// HTTP/SSE transport at <c>/mcp</c>. Tools and resources mirror the REST
/// surface area so any MCP-capable agent (Claude Code, Cursor, Copilot)
/// can drive Hiveboard workflows without bespoke API integration.
///
/// Authentication piggybacks on the existing <c>X-Api-Key</c> handler used
/// by the REST API:
///   * Clients pass <c>X-Api-Key: &lt;agent or admin key&gt;</c> in the HTTP
///     request headers (the standard MCP client config flow).
///   * If a request reaches <c>/mcp</c> without that header, the
///     <see cref="HiveboardMcpApiKeyFallbackMiddleware"/> injects the value
///     of the <c>HIVEBOARD_API_KEY</c> configuration entry (or environment
///     variable) so the server can be self-hosted with a default credential
///     for local development or stdio-style proxy setups.
///
/// Smoke test (see Mcp/README-smoke-test.md for full instructions):
///   1. dotnet run --project src/Hiveboard.Api
///   2. npx @modelcontextprotocol/inspector
///      → Connect: SSE, http://localhost:&lt;port&gt;/mcp,
///        Authorization tab → set header X-Api-Key
///   3. tools/list → expect 9 hiveboard_* tools.
///   4. resources/list → expect 3 hiveboard:// templates.
///   5. Invoke hiveboard_list_tasks with a known projectId → success.
///   6. Invoke hiveboard_get_task with a malformed GUID → structured
///      McpException with error code "invalid_argument".
/// </summary>
public static class HiveboardMcpServer
{
    public const string DefaultEndpointPath = "/mcp";
    public const string ApiKeyHeaderName = "X-Api-Key";
    public const string ApiKeyConfigurationName = "HIVEBOARD_API_KEY";

    public static IServiceCollection AddHiveboardMcpServer(this IServiceCollection services)
    {
        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "hiveboard-mcp",
                    Version = "1.0.0",
                    Title = "Hiveboard MCP Server",
                    Description =
                        "Headless project management for multi-agent software workflows. " +
                        "Exposes Hiveboard tasks, notes, decisions, dependencies, and notifications " +
                        "as MCP tools and resources."
                };
            })
            .WithHttpTransport(transportOptions =>
            {
                // Stateless lets us scale horizontally and avoids SDK warnings about
                // server→client features (sampling/elicitation) we do not use.
                transportOptions.Stateless = true;
            })
            .WithTools<McpTools>()
            .WithResources<McpResources>();

        return services;
    }
}
