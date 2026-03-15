using Hiveboard.Core.Enums;

namespace Hiveboard.Core.Entities;

public class DecisionRecord
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid AgentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DecisionStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Project? Project { get; set; }
    public AgentTask? Task { get; set; }
    public Agent? Agent { get; set; }
}
