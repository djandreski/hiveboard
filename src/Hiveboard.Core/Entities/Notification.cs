using Hiveboard.Core.Enums;

namespace Hiveboard.Core.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public NotificationType Type { get; set; }
    public Guid TaskId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Agent? Agent { get; set; }
    public AgentTask? Task { get; set; }
}
