using Hiveboard.Core.Enums;

namespace Hiveboard.Api.Auth;

public class AgentContext
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public AgentType AgentType { get; set; }
    public Guid OrganizationId { get; set; }
    public string? OrganizationScopeError { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsCoordinator => IsAdmin;
    public bool HasOrganizationScope => OrganizationId != Guid.Empty;
    public bool IsOrchestrator => AgentId != Guid.Empty && AgentType == AgentType.Orchestrator;
}
