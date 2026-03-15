namespace Hiveboard.Core.Entities;

public class TaskEvent
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid AgentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public AgentTask? Task { get; set; }
    public Agent? Agent { get; set; }
}
