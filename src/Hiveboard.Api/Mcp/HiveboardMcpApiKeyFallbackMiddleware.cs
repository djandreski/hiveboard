namespace Hiveboard.Api.Mcp;

/// <summary>
/// For requests targeting the MCP endpoint, if no <c>X-Api-Key</c> header
/// is present, fall back to the value of the <c>HIVEBOARD_API_KEY</c>
/// configuration entry / environment variable so the MCP server can be
/// run with a server-side default credential (matches the spec for stdio
/// transport setups and supports local smoke testing).
/// </summary>
internal sealed class HiveboardMcpApiKeyFallbackMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _endpointPath;

    public HiveboardMcpApiKeyFallbackMiddleware(RequestDelegate next, string endpointPath)
    {
        _next = next;
        _endpointPath = endpointPath;
    }

    public Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (context.Request.Path.StartsWithSegments(_endpointPath, StringComparison.OrdinalIgnoreCase) &&
            !context.Request.Headers.ContainsKey(HiveboardMcpServer.ApiKeyHeaderName))
        {
            var fallbackKey = configuration[HiveboardMcpServer.ApiKeyConfigurationName]
                ?? Environment.GetEnvironmentVariable(HiveboardMcpServer.ApiKeyConfigurationName);

            if (!string.IsNullOrWhiteSpace(fallbackKey))
            {
                context.Request.Headers[HiveboardMcpServer.ApiKeyHeaderName] = fallbackKey;
            }
        }

        return _next(context);
    }
}

internal static class HiveboardMcpApiKeyFallbackMiddlewareExtensions
{
    public static IApplicationBuilder UseHiveboardMcpApiKeyFallback(
        this IApplicationBuilder app,
        string endpointPath)
    {
        return app.UseMiddleware<HiveboardMcpApiKeyFallbackMiddleware>(endpointPath);
    }
}
