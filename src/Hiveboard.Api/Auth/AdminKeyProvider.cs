using System.Security.Cryptography;
using System.Text;
using Hiveboard.Core.Entities;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Api.Auth;

public class AdminKeyProvider
{
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

            var existing = await _db.AdminKeys.FirstOrDefaultAsync();
            if (existing is not null)
            {
                existing.KeyHash = hash;
                existing.KeyPrefix = prefix;
                existing.CreatedAt = DateTimeOffset.UtcNow;
                existing.LastUsedAt = null;
            }
            else
            {
                _db.AdminKeys.Add(new AdminKey
                {
                    Id = Guid.NewGuid(),
                    KeyHash = hash,
                    KeyPrefix = prefix,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Admin API Key loaded from environment variable (prefix: {Prefix})", prefix);
            return;
        }

        var adminKey = await _db.AdminKeys.FirstOrDefaultAsync();
        if (adminKey is not null)
        {
            return; // Already initialized
        }

        var plaintext = GenerateAdminKey();
        var newHash = HashKey(plaintext);
        var newPrefix = plaintext[..12];

        _db.AdminKeys.Add(new AdminKey
        {
            Id = Guid.NewGuid(),
            KeyHash = newHash,
            KeyPrefix = newPrefix,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync();

        _logger.LogWarning("Admin API Key: {AdminKey} (save this, it won't be shown again)", plaintext);
        Console.WriteLine($"\n  Admin API Key: {plaintext}\n  (save this, it won't be shown again)\n");
    }

    public async Task<bool> ValidateAdminKeyAsync(string key)
    {
        var hash = HashKey(key);
        var adminKey = await _db.AdminKeys.FirstOrDefaultAsync();
        if (adminKey is null) return false;

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(adminKey.KeyHash),
                Encoding.UTF8.GetBytes(hash)))
            return false;

        adminKey.LastUsedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<AdminKey?> GetAdminKeyInfoAsync()
    {
        return await _db.AdminKeys.FirstOrDefaultAsync();
    }

    public async Task<string> RotateAdminKeyAsync()
    {
        var plaintext = GenerateAdminKey();
        var hash = HashKey(plaintext);
        var prefix = plaintext[..12];

        var existing = await _db.AdminKeys.FirstOrDefaultAsync();
        if (existing is not null)
        {
            existing.KeyHash = hash;
            existing.KeyPrefix = prefix;
            existing.CreatedAt = DateTimeOffset.UtcNow;
            existing.LastUsedAt = null;
        }
        else
        {
            _db.AdminKeys.Add(new AdminKey
            {
                Id = Guid.NewGuid(),
                KeyHash = hash,
                KeyPrefix = prefix,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync();
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
}
