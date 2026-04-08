using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Api.Application;

public interface ICoordinatorScopeResolver
{
    Task<CoordinatorScopeResolution> ResolveAsync(CancellationToken cancellationToken = default);
}

public sealed record CoordinatorScopeResolution(Guid? OrganizationId, string? Error)
{
    public bool IsResolved => OrganizationId.HasValue && OrganizationId.Value != Guid.Empty;
}

public sealed class CoordinatorScopeResolver : ICoordinatorScopeResolver
{
    public const string DefaultOrganizationName = "Default Organization";
    private const string LegacyDefaultOrganizationName = "Default Org";

    private readonly HiveboardDbContext _db;

    public CoordinatorScopeResolver(HiveboardDbContext db)
    {
        _db = db;
    }

    public async Task<CoordinatorScopeResolution> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await _db.Organizations
            .AsNoTracking()
            .Select(organization => new CoordinatorOrganizationReference(
                organization.Id,
                organization.Name,
                organization.CreatedAt))
            .ToListAsync(cancellationToken);

        organizations = organizations
            .OrderBy(organization => organization.CreatedAt)
            .ToList();

        if (organizations.Count == 0)
        {
            return new CoordinatorScopeResolution(
                null,
                "Coordinator credential could not be mapped to an organization because no organization exists yet. Create or seed the default organization first.");
        }

        if (organizations.Count == 1)
            return new CoordinatorScopeResolution(organizations[0].Id, null);

        var defaultMatches = organizations
            .Where(organization => IsDefaultOrganizationName(organization.Name))
            .ToList();

        if (defaultMatches.Count == 1)
            return new CoordinatorScopeResolution(defaultMatches[0].Id, null);

        if (defaultMatches.Count > 1)
        {
            return new CoordinatorScopeResolution(
                null,
                $"Coordinator credential could not be mapped to an organization because multiple default organizations were found. Keep exactly one organization named '{DefaultOrganizationName}' in the self-hosted MVP.");
        }

        return new CoordinatorScopeResolution(
            null,
            $"Coordinator credential could not be mapped to an organization. Self-hosted MVP requires exactly one organization or one named '{DefaultOrganizationName}'.");
    }

    private static bool IsDefaultOrganizationName(string name) =>
        string.Equals(name, DefaultOrganizationName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, LegacyDefaultOrganizationName, StringComparison.OrdinalIgnoreCase);

    private sealed record CoordinatorOrganizationReference(Guid Id, string Name, DateTimeOffset CreatedAt);
}
