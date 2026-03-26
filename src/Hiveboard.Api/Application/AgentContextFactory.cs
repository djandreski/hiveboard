using System.Security.Claims;
using Hiveboard.Api.Auth;
using Hiveboard.Core.Enums;

namespace Hiveboard.Api.Application;

public static class AgentContextFactory
{
    public static AgentContext Create(ClaimsPrincipal? user)
    {
        var agentContext = new AgentContext
        {
            IsAdmin = string.Equals(
                user?.FindFirst("IsAdmin")?.Value,
                "true",
                StringComparison.OrdinalIgnoreCase),
            AgentName = user?.FindFirst("AgentName")?.Value ?? string.Empty
        };

        if (Guid.TryParse(user?.FindFirst("AgentId")?.Value, out var agentId))
            agentContext.AgentId = agentId;

        if (Enum.TryParse<AgentType>(user?.FindFirst("AgentType")?.Value, ignoreCase: true, out var agentType))
            agentContext.AgentType = agentType;

        if (Guid.TryParse(user?.FindFirst("OrganizationId")?.Value, out var organizationId))
            agentContext.OrganizationId = organizationId;

        return agentContext;
    }
}
