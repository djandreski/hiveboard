using Hiveboard.Api.Auth;
using Hiveboard.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Hiveboard.Api.Application;

public static class HiveboardApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddHiveboardApplication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAgentContextAccessor, AgentContextAccessor>();
        services.AddScoped<AgentContext>(static serviceProvider => serviceProvider.GetRequiredService<IAgentContextAccessor>().Current);

        services.AddScoped<IAgentAccessGuard, AgentAccessGuard>();
        services.AddScoped<ICoordinatorScopeResolver, CoordinatorScopeResolver>();
        services.AddScoped<AdminKeyProvider>();
        services.AddScoped<IDependencyRepository, EfCoreDependencyRepository>();
        services.AddScoped<INotificationRepository, EfCoreNotificationRepository>();
        services.AddScoped<ITaskContextRepository, EfCoreTaskContextRepository>();
        services.AddScoped<DependencyService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<TaskContextService>();
        services.AddScoped<TaskStateMachine>();
        services.AddScoped<AgentApplicationService>();
        services.AddScoped<DependencyApplicationService>();
        services.AddScoped<ProjectApplicationService>();
        services.AddScoped<EpicApplicationService>();
        services.AddScoped<TaskApplicationService>();
        services.AddScoped<NotesAndDecisionsApplicationService>();
        services.AddScoped<NotificationApplicationService>();

        return services;
    }
}
