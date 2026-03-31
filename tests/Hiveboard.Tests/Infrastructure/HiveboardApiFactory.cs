using Hiveboard.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hiveboard.Tests.Infrastructure;

internal sealed class HiveboardApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"hiveboard-tests-{Guid.NewGuid():N}.db");
    private readonly object _startupLock = new();
    private bool _started;

    public string AdminApiKey { get; } =
        "hb_adm_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(GetApiContentRoot());
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "sqlite",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}",
                ["HIVEBOARD_ADMIN_KEY"] = AdminApiKey
            });
        });
    }

    public HttpClient CreateAnonymousClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient CreateAuthenticatedClient(string apiKey)
    {
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    public async Task SeedAsync(Action<HiveboardDbContext> seed)
    {
        EnsureServerStarted();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HiveboardDbContext>();
        await ResetSeededDataAsync(db);
        seed(db);
        await db.SaveChangesAsync();
    }

    public async Task<TResult> QueryAsync<TResult>(Func<HiveboardDbContext, Task<TResult>> query)
    {
        EnsureServerStarted();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HiveboardDbContext>();
        return await query(db);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteDatabaseIfPresent();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DeleteDatabaseIfPresent();
    }

    private void EnsureServerStarted()
    {
        if (_started)
            return;

        lock (_startupLock)
        {
            if (_started)
                return;

            using var _ = CreateAnonymousClient();
            _started = true;
        }
    }

    private static string GetApiContentRoot()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "Hiveboard.Api"));
    }

    private void DeleteDatabaseIfPresent()
    {
        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task ResetSeededDataAsync(HiveboardDbContext db)
    {
        if (!await db.Organizations.AnyAsync())
            return;

        db.Notifications.RemoveRange(db.Notifications);
        db.TaskEvents.RemoveRange(db.TaskEvents);
        db.TaskNotes.RemoveRange(db.TaskNotes);
        db.TaskDependencies.RemoveRange(db.TaskDependencies);
        db.DecisionRecords.RemoveRange(db.DecisionRecords);
        db.AgentTasks.RemoveRange(db.AgentTasks);
        db.Epics.RemoveRange(db.Epics);
        db.Projects.RemoveRange(db.Projects);
        db.Agents.RemoveRange(db.Agents);
        db.Organizations.RemoveRange(db.Organizations);
        await db.SaveChangesAsync();
    }
}
