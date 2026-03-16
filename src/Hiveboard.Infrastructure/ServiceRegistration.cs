using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hiveboard.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddHiveboardInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        var provider = config["DatabaseProvider"]?.ToLowerInvariant() ?? "sqlite";
        var connectionString = config.GetConnectionString("DefaultConnection")
                               ?? "Data Source=hiveboard.db";

        services.AddDbContext<HiveboardDbContext>(options =>
        {
            if (provider == "postgresql")
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        return services;
    }
}
