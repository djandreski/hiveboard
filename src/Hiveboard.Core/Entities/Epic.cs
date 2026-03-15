using Hiveboard.Core.Enums;

namespace Hiveboard.Core.Entities;

public class Epic
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EpicStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Project? Project { get; set; }
    public ICollection<AgentTask> Tasks { get; set; } = new List<AgentTask>();
}
