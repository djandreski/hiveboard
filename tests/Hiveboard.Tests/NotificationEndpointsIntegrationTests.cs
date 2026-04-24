using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Tests;

public class NotificationEndpointsIntegrationTests
{
    private const string WorkerApiKey =
        "hb_sk_notification_worker_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherWorkerApiKey =
        "hb_sk_notification_other_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ListAndAck_ReturnsUnacknowledgedNotifications_ForCallingWorker()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var worker = IntegrationTestData.CreateAgent(organization.Id, "Worker One", AgentType.Worker, WorkerApiKey);
        var otherWorker = IntegrationTestData.CreateAgent(organization.Id, "Worker Two", AgentType.Worker, OtherWorkerApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Notification Project");

        var workerTask = new AgentTask
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Worker Task",
            Description = string.Empty,
            Status = TaskStatusEnum.Assigned,
            AssignedAgentId = worker.Id,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var otherTask = new AgentTask
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Other Worker Task",
            Description = string.Empty,
            Status = TaskStatusEnum.Assigned,
            AssignedAgentId = otherWorker.Id,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var olderNotification = new Notification
        {
            Id = Guid.NewGuid(),
            AgentId = worker.Id,
            Type = NotificationType.TaskAssigned,
            TaskId = workerTask.Id,
            Message = "Task 'Worker Task' has been assigned to you",
            IsAcknowledged = false,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var newerNotification = new Notification
        {
            Id = Guid.NewGuid(),
            AgentId = worker.Id,
            Type = NotificationType.DependencyResolved,
            TaskId = workerTask.Id,
            Message = "Dependency resolved",
            IsAcknowledged = false,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var acknowledgedNotification = new Notification
        {
            Id = Guid.NewGuid(),
            AgentId = worker.Id,
            Type = NotificationType.TaskAssigned,
            TaskId = workerTask.Id,
            Message = "Already ack'd",
            IsAcknowledged = true,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
        };
        var otherWorkersNotification = new Notification
        {
            Id = Guid.NewGuid(),
            AgentId = otherWorker.Id,
            Type = NotificationType.TaskAssigned,
            TaskId = otherTask.Id,
            Message = "Not for you",
            IsAcknowledged = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.AddRange(worker, otherWorker);
            db.Projects.Add(project);
            db.AgentTasks.AddRange(workerTask, otherTask);
            db.Notifications.AddRange(
                olderNotification,
                newerNotification,
                acknowledgedNotification,
                otherWorkersNotification);
        });

        using var workerClient = app.CreateAuthenticatedClient(WorkerApiKey);

        var listResponse = await workerClient.GetAsync("/api/v1/agents/me/notifications");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listPayload = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var notifications = listPayload.RootElement.EnumerateArray().ToList();

        Assert.Equal(2, notifications.Count);

        // Newest first
        Assert.Equal(newerNotification.Id, notifications[0].GetProperty("id").GetGuid());
        Assert.Equal("DependencyResolved", notifications[0].GetProperty("type").GetString());
        Assert.Equal(workerTask.Id, notifications[0].GetProperty("taskId").GetGuid());
        Assert.Equal("Worker Task", notifications[0].GetProperty("taskTitle").GetString());
        Assert.False(notifications[0].GetProperty("isAcknowledged").GetBoolean());

        Assert.Equal(olderNotification.Id, notifications[1].GetProperty("id").GetGuid());
        Assert.Equal("TaskAssigned", notifications[1].GetProperty("type").GetString());

        // Acknowledge the older one
        var ackResponse = await workerClient.PostAsync(
            $"/api/v1/agents/me/notifications/{olderNotification.Id}/ack",
            content: null);
        Assert.Equal(HttpStatusCode.OK, ackResponse.StatusCode);

        // Re-list should only return the newer one
        var listAfterAck = await workerClient.GetAsync("/api/v1/agents/me/notifications");
        Assert.Equal(HttpStatusCode.OK, listAfterAck.StatusCode);

        using var listAfterAckPayload = JsonDocument.Parse(await listAfterAck.Content.ReadAsStringAsync());
        var remaining = listAfterAckPayload.RootElement.EnumerateArray().ToList();
        Assert.Single(remaining);
        Assert.Equal(newerNotification.Id, remaining[0].GetProperty("id").GetGuid());

        // Confirm DB state
        var persisted = await app.QueryAsync(db => db.Notifications
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == olderNotification.Id));
        Assert.True(persisted.IsAcknowledged);
    }

    [Fact]
    public async Task Acknowledge_ReturnsNotFound_WhenNotificationBelongsToAnotherAgent()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var worker = IntegrationTestData.CreateAgent(organization.Id, "Caller", AgentType.Worker, WorkerApiKey);
        var otherWorker = IntegrationTestData.CreateAgent(organization.Id, "Owner", AgentType.Worker, OtherWorkerApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Scope Project");

        var task = new AgentTask
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Owner Task",
            Description = string.Empty,
            Status = TaskStatusEnum.Assigned,
            AssignedAgentId = otherWorker.Id,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var foreignNotification = new Notification
        {
            Id = Guid.NewGuid(),
            AgentId = otherWorker.Id,
            Type = NotificationType.TaskAssigned,
            TaskId = task.Id,
            Message = "For other worker",
            IsAcknowledged = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.AddRange(worker, otherWorker);
            db.Projects.Add(project);
            db.AgentTasks.Add(task);
            db.Notifications.Add(foreignNotification);
        });

        using var workerClient = app.CreateAuthenticatedClient(WorkerApiKey);

        var ackResponse = await workerClient.PostAsync(
            $"/api/v1/agents/me/notifications/{foreignNotification.Id}/ack",
            content: null);
        Assert.Equal(HttpStatusCode.NotFound, ackResponse.StatusCode);

        var persisted = await app.QueryAsync(db => db.Notifications
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == foreignNotification.Id));
        Assert.False(persisted.IsAcknowledged);

        var missingAckResponse = await workerClient.PostAsync(
            $"/api/v1/agents/me/notifications/{Guid.NewGuid()}/ack",
            content: null);
        Assert.Equal(HttpStatusCode.NotFound, missingAckResponse.StatusCode);
    }

    [Fact]
    public async Task CoordinatorKey_ReceivesCoordinatorNotifications_FromBlockedTransition()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var worker = IntegrationTestData.CreateAgent(organization.Id, "Worker One", AgentType.Worker, WorkerApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Coordinator Inbox Project");

        var task = new AgentTask
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Setup Auth",
            Description = string.Empty,
            Status = TaskStatusEnum.InProgress,
            AssignedAgentId = worker.Id,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await app.SeedAsync(db =>
        {
            db.Organizations.Add(organization);
            db.Agents.Add(worker);
            db.Projects.Add(project);
            db.AgentTasks.Add(task);
        });

        using var workerClient = app.CreateAuthenticatedClient(WorkerApiKey);
        using var coordinatorClient = app.CreateCoordinatorClient();

        var blockedResponse = await workerClient.PatchAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/status",
            new { status = "Blocked", blockedReason = "waiting for API design decision" });
        Assert.Equal(HttpStatusCode.OK, blockedResponse.StatusCode);

        var listResponse = await coordinatorClient.GetAsync("/api/v1/agents/me/notifications");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listPayload = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var notifications = listPayload.RootElement.EnumerateArray().ToList();

        Assert.Contains(notifications, notification =>
            notification.GetProperty("type").GetString() == "TaskBlocked" &&
            notification.GetProperty("taskId").GetGuid() == task.Id &&
            notification.GetProperty("taskTitle").GetString() == "Setup Auth");

        var blockedNotification = notifications.First(notification =>
            notification.GetProperty("type").GetString() == "TaskBlocked");
        var blockedId = blockedNotification.GetProperty("id").GetGuid();

        var ackResponse = await coordinatorClient.PostAsync(
            $"/api/v1/agents/me/notifications/{blockedId}/ack",
            content: null);
        Assert.Equal(HttpStatusCode.OK, ackResponse.StatusCode);

        var persisted = await app.QueryAsync(db => db.Notifications
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == blockedId));
        Assert.True(persisted.IsAcknowledged);
    }
}
