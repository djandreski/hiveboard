using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Api.Auth;

public sealed record AdminKeyInfo(
    string KeyPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);

public class AdminKeyProvider
{
    private const string SelectAdminKeySql = """
        SELECT "Id", "KeyHash", "KeyPrefix", "CreatedAt", "LastUsedAt"
        FROM "AdminKeys"
        LIMIT 1;
        """;

    private readonly HiveboardDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminKeyProvider> _logger;

    public AdminKeyProvider(HiveboardDbContext db, IConfiguration config, ILogger<AdminKeyProvider> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task EnsureAdminKeyAsync()
    {
        var envKey = _config["HIVEBOARD_ADMIN_KEY"]
                     ?? Environment.GetEnvironmentVariable("HIVEBOARD_ADMIN_KEY");

        if (!string.IsNullOrEmpty(envKey))
        {
            var hash = HashKey(envKey);
            var prefix = envKey.Length >= 12 ? envKey[..12] : envKey;

            var existing = await GetStoredAdminKeyAsync();
            if (existing is not null)
            {
                await UpdateAdminKeyAsync(
                    existing.Id,
                    hash,
                    prefix,
                    DateTimeOffset.UtcNow,
                    lastUsedAt: null);
            }
            else
            {
                await InsertAdminKeyAsync(
                    Guid.NewGuid(),
                    hash,
                    prefix,
                    DateTimeOffset.UtcNow,
                    lastUsedAt: null);
            }

            _logger.LogInformation("Admin API Key loaded from environment variable (prefix: {Prefix})", prefix);
            return;
        }

        var adminKey = await GetStoredAdminKeyAsync();
        if (adminKey is not null)
        {
            return; // Already initialized
        }

        var plaintext = GenerateAdminKey();
        var newHash = HashKey(plaintext);
        var newPrefix = plaintext[..12];

        await InsertAdminKeyAsync(
            Guid.NewGuid(),
            newHash,
            newPrefix,
            DateTimeOffset.UtcNow,
            lastUsedAt: null);

        _logger.LogWarning(
            "Admin API Key auto-generated for bootstrap (prefix: {Prefix}). Plaintext is not emitted to logs/stdout. " +
            "Set HIVEBOARD_ADMIN_KEY and restart to override with a known key.",
            newPrefix);
    }

    public async Task<bool> ValidateAdminKeyAsync(string key)
    {
        var hash = HashKey(key);
        var adminKey = await GetStoredAdminKeyAsync();
        if (adminKey is null) return false;

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(adminKey.KeyHash),
                Encoding.UTF8.GetBytes(hash)))
            return false;

        await UpdateLastUsedAtAsync(adminKey.Id, DateTimeOffset.UtcNow);
        return true;
    }

    public async Task<AdminKeyInfo?> GetAdminKeyInfoAsync()
    {
        var adminKey = await GetStoredAdminKeyAsync();
        return adminKey is null
            ? null
            : new AdminKeyInfo(adminKey.KeyPrefix, adminKey.CreatedAt, adminKey.LastUsedAt);
    }

    public async Task<string> RotateAdminKeyAsync()
    {
        var plaintext = GenerateAdminKey();
        var hash = HashKey(plaintext);
        var prefix = plaintext[..12];

        var existing = await GetStoredAdminKeyAsync();
        if (existing is not null)
        {
            await UpdateAdminKeyAsync(
                existing.Id,
                hash,
                prefix,
                DateTimeOffset.UtcNow,
                lastUsedAt: null);
        }
        else
        {
            await InsertAdminKeyAsync(
                Guid.NewGuid(),
                hash,
                prefix,
                DateTimeOffset.UtcNow,
                lastUsedAt: null);
        }

        return plaintext;
    }

    public static string GenerateAdminKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "hb_adm_" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateAgentKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "hb_sk_" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string HashKey(string key)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private async Task<StoredAdminKey?> GetStoredAdminKeyAsync()
    {
        await using var command = await CreateOpenCommandAsync(SelectAdminKeySql);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new StoredAdminKey(
            ReadGuid(reader, 0),
            reader.GetString(1),
            reader.GetString(2),
            ReadDateTimeOffset(reader, 3),
            reader.IsDBNull(4) ? null : ReadDateTimeOffset(reader, 4));
    }

    private async Task InsertAdminKeyAsync(
        Guid id,
        string keyHash,
        string keyPrefix,
        DateTimeOffset createdAt,
        DateTimeOffset? lastUsedAt)
    {
        await using var command = await CreateOpenCommandAsync("""
            INSERT INTO "AdminKeys" ("Id", "KeyHash", "KeyPrefix", "CreatedAt", "LastUsedAt")
            VALUES (@id, @keyHash, @keyPrefix, @createdAt, @lastUsedAt);
            """);
        AddParameter(command, "@id", id);
        AddParameter(command, "@keyHash", keyHash);
        AddParameter(command, "@keyPrefix", keyPrefix);
        AddParameter(command, "@createdAt", createdAt);
        AddParameter(command, "@lastUsedAt", lastUsedAt);
        await command.ExecuteNonQueryAsync();
    }

    private async Task UpdateAdminKeyAsync(
        Guid id,
        string keyHash,
        string keyPrefix,
        DateTimeOffset createdAt,
        DateTimeOffset? lastUsedAt)
    {
        await using var command = await CreateOpenCommandAsync("""
            UPDATE "AdminKeys"
            SET "KeyHash" = @keyHash,
                "KeyPrefix" = @keyPrefix,
                "CreatedAt" = @createdAt,
                "LastUsedAt" = @lastUsedAt
            WHERE "Id" = @id;
            """);
        AddParameter(command, "@id", id);
        AddParameter(command, "@keyHash", keyHash);
        AddParameter(command, "@keyPrefix", keyPrefix);
        AddParameter(command, "@createdAt", createdAt);
        AddParameter(command, "@lastUsedAt", lastUsedAt);
        await command.ExecuteNonQueryAsync();
    }

    private async Task UpdateLastUsedAtAsync(Guid id, DateTimeOffset lastUsedAt)
    {
        await using var command = await CreateOpenCommandAsync("""
            UPDATE "AdminKeys"
            SET "LastUsedAt" = @lastUsedAt
            WHERE "Id" = @id;
            """);
        AddParameter(command, "@id", id);
        AddParameter(command, "@lastUsedAt", lastUsedAt);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<DbCommand> CreateOpenCommandAsync(string sql)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static Guid ReadGuid(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            Guid guid => guid,
            string text => Guid.Parse(text),
            byte[] bytes => new Guid(bytes),
            _ => Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
        };
    }

    private static DateTimeOffset ReadDateTimeOffset(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture),
            _ => DateTimeOffset.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, CultureInfo.InvariantCulture)
        };
    }

    private sealed record StoredAdminKey(
        Guid Id,
        string KeyHash,
        string KeyPrefix,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastUsedAt);
}
