using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/agents/me/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", ListMyNotifications)
            .WithName("ListMyNotifications")
            .WithSummary("List unacknowledged notifications for the caller")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Returns unacknowledged notifications addressed to the caller, ordered by creation time descending.")
            .Produces<List<NotificationResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/ack", AcknowledgeNotification)
            .WithName("AcknowledgeNotification")
            .WithSummary("Acknowledge a notification")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Marks the notification as acknowledged when it belongs to the caller.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static Task<IResult> ListMyNotifications(NotificationApplicationService applicationService)
        => applicationService.ListMyNotificationsAsync();

    private static Task<IResult> AcknowledgeNotification(
        Guid id,
        NotificationApplicationService applicationService)
        => applicationService.AcknowledgeAsync(id);
}
