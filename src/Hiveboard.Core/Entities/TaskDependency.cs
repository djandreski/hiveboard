using Hiveboard.Core.Enums;

namespace Hiveboard.Core.Entities;

public class TaskDependency
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }
    public DependencyType Type { get; set; }

    public AgentTask? Task { get; set; }
    public AgentTask? DependsOnTask { get; set; }
}
