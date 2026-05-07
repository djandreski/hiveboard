using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using ModelContextProtocol;

namespace Hiveboard.Api.Mcp;

/// <summary>
/// Bridges Hiveboard application services (which return ASP.NET Core
/// <see cref="IResult"/>) to MCP tool return values.
///
/// We pattern-match on the well-known <c>Microsoft.AspNetCore.Http.HttpResults</c>
/// types instead of executing the result against a synthetic
/// <c>HttpContext</c> — that avoided having to satisfy
/// <c>WriteAsJsonAsync</c>'s dependency on <c>ILoggerFactory</c> and
/// <c>JsonOptions</c> from a request DI container.
///
/// Successful results (2xx) are returned as a JSON-serialized string. The
/// MCP SDK puts that string into a <c>TextContentBlock</c> on the
/// <c>CallToolResult</c> with <c>IsError</c> defaulting to null/false.
/// Failure results (4xx/5xx) become <see cref="McpException"/>s with a
/// machine-readable code prefix so MCP clients can branch on them:
///   * <c>[invalid_argument]</c> — 400 Bad Request
///   * <c>[unauthorized]</c>     — 401 Unauthorized
///   * <c>[forbidden]</c>        — 403 Forbidden
///   * <c>[not_found]</c>        — 404 Not Found
///   * <c>[conflict]</c>         — 409 Conflict
///   * <c>[internal_error]</c>   — 5xx
/// </summary>
internal static class McpResultConverter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task<string> ToJsonStringAsync(IResult result, CancellationToken cancellationToken = default)
    {
        var (statusCode, payload) = ExtractStatusAndPayload(result);

        if (statusCode is >= 200 and < 300)
        {
            return Task.FromResult(SerializePayload(payload));
        }

        var errorCode = MapErrorCode(statusCode);
        var (message, detailJson) = ExtractMessageAndDetail(payload);

        var formatted = detailJson is null
            ? $"[{errorCode}] {message ?? $"Hiveboard returned HTTP {statusCode}."}"
            : $"[{errorCode}] {message ?? $"Hiveboard returned HTTP {statusCode}."} :: {detailJson}";

        throw new McpException(formatted);
    }

    private static string SerializePayload(object? payload)
    {
        if (payload is null)
            return "null";

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static (int StatusCode, object? Payload) ExtractStatusAndPayload(IResult result)
    {
        var statusCode = result is IStatusCodeHttpResult statusResult && statusResult.StatusCode.HasValue
            ? statusResult.StatusCode.Value
            : StatusCodes.Status200OK;

        var payload = result is IValueHttpResult valueResult ? valueResult.Value : null;

        return (statusCode, payload);
    }

    private static string MapErrorCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "invalid_argument",
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status409Conflict => "conflict",
        StatusCodes.Status422UnprocessableEntity => "invalid_argument",
        _ when statusCode >= 500 => "internal_error",
        _ => "error"
    };

    private static (string? Message, string? DetailJson) ExtractMessageAndDetail(object? payload)
    {
        if (payload is null)
            return (null, null);

        var element = JsonSerializer.SerializeToElement(payload, SerializerOptions);

        if (element.ValueKind != JsonValueKind.Object)
            return (null, element.GetRawText());

        string? message = null;
        if (element.TryGetProperty("error", out var errorProperty) &&
            errorProperty.ValueKind == JsonValueKind.String)
        {
            message = errorProperty.GetString();
        }
        else if (element.TryGetProperty("message", out var messageProperty) &&
                 messageProperty.ValueKind == JsonValueKind.String)
        {
            message = messageProperty.GetString();
        }

        return (message, element.GetRawText());
    }
}
