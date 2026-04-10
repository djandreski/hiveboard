using System.Security.Claims;
using Hiveboard.Core.Enums;
using Hiveboard.Core.Services;

namespace Hiveboard.Api.Application;

public static class AgentContextFactory
{
    public static AgentContext Create(ClaimsPrincipal? user)
    {
        var isAdmin = string.Equals(
            user?.FindFirst("IsAdmin")?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

        var agentContext = new AgentContext
        {
            IsAdmin = isAdmin,
            AgentName = user?.FindFirst("AgentName")?.Value ?? (isAdmin ? "Coordinator" : string.Empty),
            OrganizationScopeError = user?.FindFirst("OrganizationScopeError")?.Value
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
