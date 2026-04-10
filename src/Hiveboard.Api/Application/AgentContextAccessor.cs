using Hiveboard.Core.Services;

namespace Hiveboard.Api.Application;

public interface IAgentContextAccessor
{
    AgentContext Current { get; }
}

public sealed class AgentContextAccessor : IAgentContextAccessor
{
    public AgentContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        Current = AgentContextFactory.Create(httpContextAccessor.HttpContext?.User);
    }

    public AgentContext Current { get; }
}
