using Hiveboard.Core.Enums;

namespace Hiveboard.Core.Entities;

public class Project
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? OrchestratorAgentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Organization? Organization { get; set; }
    public Agent? OrchestratorAgent { get; set; }
    public ICollection<Agent> WorkerAgents { get; set; } = new List<Agent>();
    public ICollection<Epic> Epics { get; set; } = new List<Epic>();
    public ICollection<AgentTask> Tasks { get; set; } = new List<AgentTask>();
    public ICollection<DecisionRecord> DecisionRecords { get; set; } = new List<DecisionRecord>();
}
