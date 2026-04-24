using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Core.Services;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Api.Application;

public sealed class NotificationApplicationService
{
    private const string CoordinatorAuditAgentName = "Coordinator";

    private readonly HiveboardDbContext _db;
    private readonly AgentContext _agentContext;
    private readonly IAgentAccessGuard _accessGuard;
    private readonly NotificationService _notificationService;

    public NotificationApplicationService(
        HiveboardDbContext db,
        AgentContext agentContext,
        IAgentAccessGuard accessGuard,
        NotificationService notificationService)
    {
        _db = db;
        _agentContext = agentContext;
        _accessGuard = accessGuard;
        _notificationService = notificationService;
    }

    public async Task<IResult> ListMyNotificationsAsync()
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var callerAgentId = await ResolveCallerAgentIdAsync();
        if (callerAgentId is null)
            return Results.NotFound(new { error = "No notification inbox is available for this caller" });

        var notifications = await _notificationService.GetUnacknowledgedAsync(callerAgentId.Value);

        return Results.Ok(notifications.Select(ToResponse).ToList());
    }

    public async Task<IResult> AcknowledgeAsync(Guid notificationId)
    {
        var scopeError = _accessGuard.ValidateOrganizationScope(_agentContext);
        if (scopeError is not null)
            return scopeError;

        var callerAgentId = await ResolveCallerAgentIdAsync();
        if (callerAgentId is null)
            return Results.NotFound(new { error = "Notification not found" });

        var acknowledged = await _notificationService.AcknowledgeAsync(notificationId, callerAgentId.Value);
        if (!acknowledged)
            return Results.NotFound(new { error = "Notification not found" });

        return Results.Ok(new { id = notificationId, isAcknowledged = true });
    }

    private async Task<Guid?> ResolveCallerAgentIdAsync()
    {
        if (_agentContext.AgentId != Guid.Empty)
            return _agentContext.AgentId;

        if (!_agentContext.IsCoordinator)
            return null;

        return await _db.Agents
            .Where(agent =>
                agent.OrganizationId == _agentContext.OrganizationId &&
                agent.Name == CoordinatorAuditAgentName &&
                agent.Type == AgentType.Orchestrator &&
                agent.AgentPlatform == AgentPlatform.Custom &&
                agent.Status == AgentStatus.Inactive &&
                agent.ApiKeyHash == null)
            .Select(agent => (Guid?)agent.Id)
            .FirstOrDefaultAsync();
    }

    private static NotificationResponse ToResponse(Notification notification) =>
        new(
            notification.Id,
            notification.Type.ToString(),
            notification.TaskId,
            notification.Task?.Title ?? string.Empty,
            notification.Message,
            notification.CreatedAt,
            notification.IsAcknowledged);
}

internal sealed class EfCoreNotificationRepository : INotificationRepository
{
    private readonly HiveboardDbContext _db;

    public EfCoreNotificationRepository(HiveboardDbContext db)
    {
        _db = db;
    }

    public void Add(Notification notification)
    {
        _db.Notifications.Add(notification);
    }

    public async Task<IReadOnlyList<Notification>> GetUnacknowledgedAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _db.Notifications
            .AsNoTracking()
            .Include(notification => notification.Task)
            .Where(notification => notification.AgentId == agentId && !notification.IsAcknowledged)
            .ToListAsync(cancellationToken);

        return notifications
            .OrderByDescending(notification => notification.CreatedAt)
            .ToList();
    }

    public Task<Notification?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken = default)
        => _db.Notifications.FirstOrDefaultAsync(candidate => candidate.Id == notificationId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
