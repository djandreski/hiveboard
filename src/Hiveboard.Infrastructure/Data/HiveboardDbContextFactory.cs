using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hiveboard.Infrastructure.Data;

public class HiveboardDbContextFactory : IDesignTimeDbContextFactory<HiveboardDbContext>
{
    public HiveboardDbContext CreateDbContext(string[] args)
    {
        var provider = ReadSetting(args, "DatabaseProvider")?.ToLowerInvariant() ?? "sqlite";
        var connectionString = ReadSetting(args, "ConnectionStrings:DefaultConnection")
                               ?? "Data Source=hiveboard.db";

        var optionsBuilder = new DbContextOptionsBuilder<HiveboardDbContext>();

        if (provider == "postgresql")
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            optionsBuilder.UseSqlite(connectionString);
        }

        return new HiveboardDbContext(optionsBuilder.Options);
    }

    private static string? ReadSetting(string[] args, string key)
    {
        var normalizedArgumentKey = key.Replace(':', '_');

        foreach (var argument in args)
        {
            if (argument.StartsWith($"--{key}=", StringComparison.OrdinalIgnoreCase))
            {
                return argument[(key.Length + 3)..];
            }

            if (argument.StartsWith($"--{normalizedArgumentKey}=", StringComparison.OrdinalIgnoreCase))
            {
                return argument[(normalizedArgumentKey.Length + 3)..];
            }
        }

        return Environment.GetEnvironmentVariable(key)
               ?? Environment.GetEnvironmentVariable(key.Replace(':', '_'))
               ?? Environment.GetEnvironmentVariable(key.Replace(":", "__"));
    }
}
