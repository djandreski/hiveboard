using Hiveboard.Api.Application;
using Hiveboard.Api.Auth;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;

namespace Hiveboard.Tests.Infrastructure;

internal static class IntegrationTestData
{
    public static Organization CreateDefaultOrganization() =>
        CreateOrganization(CoordinatorScopeResolver.DefaultOrganizationName);

    public static Organization CreateOrganization(string name)
    {
        return new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Agent CreateAgent(
        Guid organizationId,
        string name,
        AgentType type,
        string apiKey,
        AgentPlatform platform = AgentPlatform.Codex,
        AgentStatus status = AgentStatus.Active)
    {
        return new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Type = type,
            AgentPlatform = platform,
            ApiKeyHash = status == AgentStatus.Active ? AdminKeyProvider.HashKey(apiKey) : null,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Project CreateProject(Guid organizationId, string name, string description = "")
    {
        return new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Description = description,
            Status = ProjectStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Epic CreateEpic(Guid projectId, string title, string description = "")
    {
        return new Epic
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title,
            Description = description,
            Status = EpicStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
