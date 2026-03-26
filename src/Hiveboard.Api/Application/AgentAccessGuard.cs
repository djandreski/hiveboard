using Hiveboard.Api.Auth;
using Hiveboard.Core.Enums;

namespace Hiveboard.Api.Application;

public interface IAgentAccessGuard
{
    IResult? ValidateOrganizationScope(AgentContext agentContext);
    IResult? ValidateOrchestratorScope(AgentContext agentContext, string forbiddenMessage);
}

public sealed class AgentAccessGuard : IAgentAccessGuard
{
    public IResult? ValidateOrganizationScope(AgentContext agentContext)
    {
        if (agentContext.IsAdmin || agentContext.OrganizationId == Guid.Empty)
            return Forbidden("Organization-scoped endpoints require an agent API key");

        return null;
    }

    public IResult? ValidateOrchestratorScope(AgentContext agentContext, string forbiddenMessage)
    {
        if (agentContext.AgentType != AgentType.Orchestrator || agentContext.AgentId == Guid.Empty)
            return Forbidden(forbiddenMessage);

        return null;
    }

    private static IResult Forbidden(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
}
