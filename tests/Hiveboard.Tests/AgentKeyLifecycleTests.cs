using Hiveboard.Api.Auth;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Tests;

public class AgentKeyLifecycleTests
{
    [Fact]
    public void Revoke_ClearsApiKeyHash_AndMarksAgentInactive()
    {
        var agent = new Agent
        {
            Status = AgentStatus.Active,
            ApiKeyHash = "existing-hash"
        };

        AgentKeyLifecycle.Revoke(agent);

        Assert.Equal(AgentStatus.Inactive, agent.Status);
        Assert.Null(agent.ApiKeyHash);
    }

    [Fact]
    public void IssueNewKey_AssignsHash_AndReactivatesAgent()
    {
        var agent = new Agent
        {
            Status = AgentStatus.Inactive
        };

        AgentKeyLifecycle.IssueNewKey(agent, "new-hash");

        Assert.Equal(AgentStatus.Active, agent.Status);
        Assert.Equal("new-hash", agent.ApiKeyHash);
    }

    [Fact]
    public async Task MultipleRevokedAgents_CanBeSavedWithANullableUniqueKeyHash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<HiveboardDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new HiveboardDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var firstAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Worker 1",
            Type = AgentType.Worker,
            AgentPlatform = AgentPlatform.Codex,
            ApiKeyHash = "hash-1",
            Status = AgentStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var secondAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Worker 2",
            Type = AgentType.Worker,
            AgentPlatform = AgentPlatform.Codex,
            ApiKeyHash = "hash-2",
            Status = AgentStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Organizations.Add(organization);
        db.Agents.AddRange(firstAgent, secondAgent);
        await db.SaveChangesAsync();

        AgentKeyLifecycle.Revoke(firstAgent);
        AgentKeyLifecycle.Revoke(secondAgent);
        await db.SaveChangesAsync();

        var revokedAgents = await db.Agents
            .OrderBy(agent => agent.Name)
            .ToListAsync();

        Assert.All(revokedAgents, agent =>
        {
            Assert.Equal(AgentStatus.Inactive, agent.Status);
            Assert.Null(agent.ApiKeyHash);
        });
    }
}
