using Hiveboard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Infrastructure.Data;

public class HiveboardDbContext : DbContext
{
    public HiveboardDbContext(DbContextOptions<HiveboardDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Epic> Epics => Set<Epic>();
    public DbSet<AgentTask> AgentTasks => Set<AgentTask>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<TaskNote> TaskNotes => Set<TaskNote>();
    public DbSet<TaskEvent> TaskEvents => Set<TaskEvent>();
    public DbSet<DecisionRecord> DecisionRecords => Set<DecisionRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HiveboardDbContext).Assembly);
    }
}
