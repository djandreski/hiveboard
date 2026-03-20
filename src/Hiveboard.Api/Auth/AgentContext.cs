using Hiveboard.Core.Enums;

namespace Hiveboard.Api.Auth;

public class AgentContext
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public AgentType AgentType { get; set; }
    public Guid OrganizationId { get; set; }
    public bool IsAdmin { get; set; }
}
