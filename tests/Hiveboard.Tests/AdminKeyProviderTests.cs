using Hiveboard.Api.Auth;
using Hiveboard.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hiveboard.Tests;

public class AdminKeyProviderTests
{
    [Fact]
    public async Task EnsureAdminKeyAsync_WhenNoEnvKey_DoesNotLeakPlaintextToLogsOrStdout()
    {
        await using var testDatabase = await CreateDbContextAsync();
        var db = testDatabase.DbContext;
        var logger = new ListLogger<AdminKeyProvider>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["HIVEBOARD_ADMIN_KEY"] = string.Empty })
            .Build();
        var provider = new AdminKeyProvider(db, config, logger);

        var originalOut = Console.Out;
        await using var capturedStdout = new StringWriter();
        Console.SetOut(capturedStdout);

        try
        {
            await provider.EnsureAdminKeyAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var storedKey = await db.AdminKeys.SingleAsync();
        Assert.StartsWith("hb_adm_", storedKey.KeyPrefix);
        Assert.Equal(12, storedKey.KeyPrefix.Length);

        var logs = string.Join(Environment.NewLine, logger.Messages);
        Assert.DoesNotContain("Admin API Key:", logs, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"hb_adm_[a-f0-9]{64}", logs);

        var stdout = capturedStdout.ToString();
        Assert.True(string.IsNullOrWhiteSpace(stdout));
        Assert.DoesNotMatch(@"hb_adm_[a-f0-9]{64}", stdout);
    }

    [Fact]
    public async Task EnsureAdminKeyAsync_WhenEnvKeyProvided_LogsPrefixButNotPlaintext()
    {
        await using var testDatabase = await CreateDbContextAsync();
        var db = testDatabase.DbContext;
        var logger = new ListLogger<AdminKeyProvider>();
        var envKey = "hb_adm_" + new string('a', 64);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["HIVEBOARD_ADMIN_KEY"] = envKey })
            .Build();
        var provider = new AdminKeyProvider(db, config, logger);

        await provider.EnsureAdminKeyAsync();

        var storedKey = await db.AdminKeys.SingleAsync();
        Assert.Equal(AdminKeyProvider.HashKey(envKey), storedKey.KeyHash);
        Assert.Equal(envKey[..12], storedKey.KeyPrefix);

        var logs = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("loaded from environment variable", logs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(envKey[..12], logs, StringComparison.Ordinal);
        Assert.DoesNotContain(envKey, logs, StringComparison.Ordinal);
    }

    private static async Task<TestDatabase> CreateDbContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HiveboardDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new HiveboardDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new TestDatabase(dbContext, connection);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        public TestDatabase(HiveboardDbContext dbContext, SqliteConnection connection)
        {
            DbContext = dbContext;
            _connection = connection;
        }

        public HiveboardDbContext DbContext { get; }

        private readonly SqliteConnection _connection;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
