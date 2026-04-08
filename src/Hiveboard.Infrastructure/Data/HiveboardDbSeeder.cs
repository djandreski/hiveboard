using System.Security.Cryptography;
using System.Text;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;

namespace Hiveboard.Infrastructure.Data;

public static class HiveboardDbSeeder
{
    private const string DefaultOrganizationName = "Default Organization";

    public static void SeedDevelopmentData(HiveboardDbContext context)
    {
        if (context.Organizations.Any())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = DefaultOrganizationName,
            CreatedAt = now
        };

        var workerA = new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Worker-1",
            Type = AgentType.Worker,
            AgentPlatform = AgentPlatform.ClaudeCode,
            ApiKeyHash = HashApiKey("dev-worker-key-123"),
            Status = AgentStatus.Active,
            CreatedAt = now
        };

        var workerB = new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Worker-2",
            Type = AgentType.Worker,
            AgentPlatform = AgentPlatform.Codex,
            ApiKeyHash = HashApiKey("dev-worker-key-456"),
            Status = AgentStatus.Active,
            CreatedAt = now
        };

        var optionalOrchestrator = new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Sample Orchestrator",
            Type = AgentType.Orchestrator,
            AgentPlatform = AgentPlatform.Custom,
            ApiKeyHash = HashApiKey("dev-orchestrator-key-789"),
            Status = AgentStatus.Active,
            CreatedAt = now
        };

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Sample Project",
            Description = "Coordinator-managed development seed project",
            Status = ProjectStatus.Active,
            CreatedAt = now,
            WorkerAgents = { workerA, workerB }
        };

        context.Organizations.Add(organization);
        context.Agents.AddRange(workerA, workerB, optionalOrchestrator);
        context.Projects.Add(project);
        context.SaveChanges();
    }

    private static string HashApiKey(string apiKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
