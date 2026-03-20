using Hiveboard.Api.Auth;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class AdminKeyEndpoints
{
    public static void MapAdminKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin/keys")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/info", GetAdminKeyInfo);
        group.MapPost("/rotate", RotateAdminKey);
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
