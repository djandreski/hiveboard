using Microsoft.Extensions.DependencyInjection;

namespace Hiveboard.Api.Application;

public static class HiveboardApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddHiveboardApplication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAgentContextAccessor, AgentContextAccessor>();
        services.AddScoped(static serviceProvider => serviceProvider.GetRequiredService<IAgentContextAccessor>().Current);

        services.AddScoped<IAgentAccessGuard, AgentAccessGuard>();
        services.AddScoped<ICoordinatorScopeResolver, CoordinatorScopeResolver>();
        services.AddScoped<AgentApplicationService>();
        services.AddScoped<ProjectApplicationService>();
        services.AddScoped<EpicApplicationService>();
        services.AddScoped<TaskApplicationService>();

        return services;
    }
}
