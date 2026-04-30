using Hiveboard.Core.Entities;
using Hiveboard.Core.Services;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hiveboard.Api.Application;

internal sealed class EfCoreTaskContextRepository : ITaskContextRepository
{
    private readonly HiveboardDbContext _db;

    public EfCoreTaskContextRepository(HiveboardDbContext db)
    {
        _db = db;
    }

    public Task<AgentTask?> GetTaskWithFullContextAsync(
        Guid taskId,
        CancellationToken cancellationToken = default) =>
        _db.AgentTasks
            .AsNoTracking()
            .AsSplitQuery()
            .Include(candidate => candidate.Project)
            .Include(candidate => candidate.Epic)
            .Include(candidate => candidate.ParentTask)
            .Include(candidate => candidate.Subtasks)
                .ThenInclude(subtask => subtask.AssignedAgent)
            .Include(candidate => candidate.Dependencies)
                .ThenInclude(dependency => dependency.DependsOnTask)
            .Include(candidate => candidate.DependentTasks)
                .ThenInclude(dependency => dependency.Task)
            .Include(candidate => candidate.Notes)
                .ThenInclude(note => note.Agent)
            .Include(candidate => candidate.Events)
                .ThenInclude(taskEvent => taskEvent.Agent)
            .Include(candidate => candidate.Decisions)
                .ThenInclude(decision => decision.Agent)
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken);
}
