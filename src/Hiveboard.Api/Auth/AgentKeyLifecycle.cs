using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;

namespace Hiveboard.Api.Auth;

public static class AgentKeyLifecycle
{
    public static void Revoke(Agent agent)
    {
        agent.Status = AgentStatus.Inactive;
        agent.ApiKeyHash = null!;
    }

    public static bool RequiresNewKeyToActivate(Agent agent)
    {
        return agent.Status == AgentStatus.Inactive;
    }

    public static void IssueNewKey(Agent agent, string keyHash)
    {
        agent.ApiKeyHash = keyHash;
        agent.Status = AgentStatus.Active;
    }
}
