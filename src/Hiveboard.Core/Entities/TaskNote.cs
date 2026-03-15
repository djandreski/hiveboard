using Hiveboard.Core.Enums;

namespace Hiveboard.Core.Entities;

public class TaskNote
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid AgentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public NoteType NoteType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AgentTask? Task { get; set; }
    public Agent? Agent { get; set; }
}
