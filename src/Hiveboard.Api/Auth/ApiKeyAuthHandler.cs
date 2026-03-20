using System.Security.Claims;
using System.Text.Encodings.Web;
using Hiveboard.Core.Enums;
using Hiveboard.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hiveboard.Api.Auth;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly HiveboardDbContext _db;
    private readonly AdminKeyProvider _adminKeyProvider;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        HiveboardDbContext db,
        AdminKeyProvider adminKeyProvider)
        : base(options, logger, encoder)
    {
        _db = db;
        _adminKeyProvider = adminKeyProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var path = Context.Request.Path.Value ?? string.Empty;

        // Exempt routes
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/dashboard", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/index.html", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
        {
            // Return no result so the request can proceed unauthenticated
            return AuthenticateResult.NoResult();
        }

        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyValues))
        {
            return AuthenticateResult.Fail("Missing X-Api-Key header");
        }

        var apiKey = apiKeyValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.Fail("Empty X-Api-Key header");
        }

        // Check admin key first
        if (await _adminKeyProvider.ValidateAdminKeyAsync(apiKey))
        {
            var adminClaims = new[]
            {
                new Claim("IsAdmin", "true"),
            };

            var adminIdentity = new ClaimsIdentity(adminClaims, Scheme.Name);
            var adminPrincipal = new ClaimsPrincipal(adminIdentity);
            return AuthenticateResult.Success(new AuthenticationTicket(adminPrincipal, Scheme.Name));
        }

        // Check agent key
        var keyHash = AdminKeyProvider.HashKey(apiKey);
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.ApiKeyHash == keyHash);

        if (agent is null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        if (agent.Status != AgentStatus.Active)
        {
            return AuthenticateResult.Fail("Agent is inactive");
        }

        // Update last seen
        agent.LastSeenAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var claims = new[]
        {
            new Claim("AgentId", agent.Id.ToString()),
            new Claim("AgentName", agent.Name),
            new Claim("AgentType", agent.Type.ToString()),
            new Claim("OrganizationId", agent.OrganizationId.ToString()),
            new Claim("IsAdmin", "false"),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
