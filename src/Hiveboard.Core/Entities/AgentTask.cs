using Hiveboard.Core.Enums;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Core.Entities;

public class AgentTask
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? EpicId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public Guid? AssignedAgentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatusEnum Status { get; set; }
    public string? BlockedReason { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Project? Project { get; set; }
    public Epic? Epic { get; set; }
    public AgentTask? ParentTask { get; set; }
    public Agent? AssignedAgent { get; set; }
    public ICollection<AgentTask> Subtasks { get; set; } = new List<AgentTask>();
    public ICollection<TaskDependency> Dependencies { get; set; } = new List<TaskDependency>();
    public ICollection<TaskDependency> DependentTasks { get; set; } = new List<TaskDependency>();
    public ICollection<TaskNote> Notes { get; set; } = new List<TaskNote>();
    public ICollection<TaskEvent> Events { get; set; } = new List<TaskEvent>();
    public ICollection<DecisionRecord> Decisions { get; set; } = new List<DecisionRecord>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
