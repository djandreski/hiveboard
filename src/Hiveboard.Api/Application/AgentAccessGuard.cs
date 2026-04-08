using Hiveboard.Api.Auth;
using Hiveboard.Core.Enums;

namespace Hiveboard.Api.Application;

public interface IAgentAccessGuard
{
    IResult? ValidateOrganizationScope(AgentContext agentContext);
    IResult? ValidateCoordinatorOrOrchestratorScope(AgentContext agentContext, string forbiddenMessage);
}

public sealed class AgentAccessGuard : IAgentAccessGuard
{
    public IResult? ValidateOrganizationScope(AgentContext agentContext)
    {
        if (agentContext.HasOrganizationScope)
            return null;

        if (agentContext.IsCoordinator && !string.IsNullOrWhiteSpace(agentContext.OrganizationScopeError))
            return Forbidden(agentContext.OrganizationScopeError);

        if (agentContext.IsCoordinator)
            return Forbidden("Coordinator credential could not be mapped to an organization.");

        return Forbidden("Authenticated agent is missing an organization scope.");

    }

    public IResult? ValidateCoordinatorOrOrchestratorScope(AgentContext agentContext, string forbiddenMessage)
    {
        if (!agentContext.IsCoordinator && !agentContext.IsOrchestrator)
            return Forbidden(forbiddenMessage);

        return null;
    }

    private static IResult Forbidden(string message) =>
        Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
}
