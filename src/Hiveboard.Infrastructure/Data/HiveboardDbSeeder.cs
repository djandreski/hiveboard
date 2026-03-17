using System.Security.Cryptography;
using System.Text;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;

namespace Hiveboard.Infrastructure.Data;

public static class HiveboardDbSeeder
{
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
            Name = "Default Org",
            CreatedAt = now
        };

        var orchestrator = new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Orchestrator",
            Type = AgentType.Orchestrator,
            AgentPlatform = AgentPlatform.Custom,
            ApiKeyHash = HashApiKey("dev-orchestrator-key-123"),
            Status = AgentStatus.Active,
            CreatedAt = now
        };

        var worker = new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Worker-1",
            Type = AgentType.Worker,
            AgentPlatform = AgentPlatform.ClaudeCode,
            ApiKeyHash = HashApiKey("dev-worker-key-456"),
            Status = AgentStatus.Active,
            CreatedAt = now
        };

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Sample Project",
            Description = "Development seed project",
            Status = ProjectStatus.Active,
            CreatedAt = now,
            OrchestratorAgent = orchestrator,
            WorkerAgents = { worker }
        };

        context.Organizations.Add(organization);
        context.Agents.AddRange(orchestrator, worker);
        context.Projects.Add(project);
        context.SaveChanges();
    }

    private static string HashApiKey(string apiKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
