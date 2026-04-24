using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;

namespace Hiveboard.Core.Services;

public interface INotificationRepository
{
    void Add(Notification notification);

    Task<IReadOnlyList<Notification>> GetUnacknowledgedAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task<Notification?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class NotificationService
{
    private readonly INotificationRepository _repository;

    public NotificationService(INotificationRepository repository)
    {
        _repository = repository;
    }

    public Notification CreateNotification(Guid agentId, NotificationType type, Guid taskId, string message)
        => CreateNotification(agentId, type, taskId, message, DateTimeOffset.UtcNow);

    public Notification CreateNotification(
        Guid agentId,
        NotificationType type,
        Guid taskId,
        string message,
        DateTimeOffset createdAt)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Type = type,
            TaskId = taskId,
            Message = message,
            IsAcknowledged = false,
            CreatedAt = createdAt
        };

        _repository.Add(notification);
        return notification;
    }

    public Task<IReadOnlyList<Notification>> GetUnacknowledgedAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
        => _repository.GetUnacknowledgedAsync(agentId, cancellationToken);

    public async Task<bool> AcknowledgeAsync(
        Guid notificationId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);

        if (notification is null || notification.AgentId != agentId)
            return false;

        if (!notification.IsAcknowledged)
        {
            notification.IsAcknowledged = true;
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
