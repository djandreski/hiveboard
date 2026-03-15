using Hiveboard.Core.Enums;

namespace Hiveboard.Core.Entities;

public class Agent
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AgentType Type { get; set; }
    public AgentPlatform AgentPlatform { get; set; }
    public string ApiKeyHash { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Organization? Organization { get; set; }
    public ICollection<Project> OrchestratedProjects { get; set; } = new List<Project>();
    public ICollection<Project> WorkerProjects { get; set; } = new List<Project>();
    public ICollection<AgentTask> AssignedTasks { get; set; } = new List<AgentTask>();
    public ICollection<TaskNote> TaskNotes { get; set; } = new List<TaskNote>();
    public ICollection<TaskEvent> TaskEvents { get; set; } = new List<TaskEvent>();
    public ICollection<DecisionRecord> DecisionRecords { get; set; } = new List<DecisionRecord>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
