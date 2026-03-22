using Hiveboard.Api.Auth;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class AdminKeyEndpoints
{
    public static void MapAdminKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin/keys")
            .WithTags("Admin")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/info", GetAdminKeyInfo)
            .WithName("GetAdminKeyInfo")
            .WithSummary("Get admin key metadata")
            .WithDescription("Auth: Admin API Key only. Returns key prefix and timestamps; never returns plaintext key.")
            .Produces<AdminKeyInfoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/rotate", RotateAdminKey)
            .WithName("RotateAdminKey")
            .WithSummary("Rotate admin API key")
            .WithDescription("Auth: Admin API Key only. Rotates the active admin key and returns the new plaintext key once.")
            .Produces<KeyRotationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetAdminKeyInfo(AdminKeyProvider adminKeyProvider)
    {
        var adminKey = await adminKeyProvider.GetAdminKeyInfoAsync();
        if (adminKey is null)
            return Results.NotFound(new { error = "Admin key not initialized" });

        return Results.Ok(new AdminKeyInfoResponse(
            adminKey.KeyPrefix,
            adminKey.CreatedAt,
            adminKey.LastUsedAt));
    }

    private static async Task<IResult> RotateAdminKey(AdminKeyProvider adminKeyProvider)
    {
        var newKey = await adminKeyProvider.RotateAdminKeyAsync();

        return Results.Ok(new KeyRotationResponse(
            newKey,
            "Admin key rotated successfully. The old key is immediately invalidated. Save the new key now."));
    }
}
