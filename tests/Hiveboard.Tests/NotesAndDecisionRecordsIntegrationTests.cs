using System.Net;
using System.Net.Http.Json;
using Hiveboard.Api.Contracts;
using Hiveboard.Core.Entities;
using Hiveboard.Core.Enums;
using Hiveboard.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = Hiveboard.Core.Enums.TaskStatus;

namespace Hiveboard.Tests;

public class NotesAndDecisionRecordsIntegrationTests
{
    private const string WorkerApiKey =
        "hb_sk_notes_worker_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ReaderApiKey =
        "hb_sk_notes_reader_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ForeignWorkerApiKey =
        "hb_sk_notes_foreign_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task TaskNotes_CanBeCreatedByWorkerAndCoordinator_AndListedWithAuthorMetadata()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var foreignOrganization = IntegrationTestData.CreateOrganization("Foreign Notes Org");
        var worker = IntegrationTestData.CreateAgent(organization.Id, "Worker Notes", AgentType.Worker, WorkerApiKey);
        var reader = IntegrationTestData.CreateAgent(organization.Id, "Reader Agent", AgentType.Worker, ReaderApiKey);
        var foreignWorker = IntegrationTestData.CreateAgent(
            foreignOrganization.Id,
            "Foreign Worker",
            AgentType.Worker,
            ForeignWorkerApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Notes Project");
        var task = CreateTask(project.Id, "Document implementation");

        await app.SeedAsync(db =>
        {
            db.Organizations.AddRange(organization, foreignOrganization);
            db.Agents.AddRange(worker, reader, foreignWorker);
            db.Projects.Add(project);
            db.AgentTasks.Add(task);
        });

        using var workerClient = app.CreateAuthenticatedClient(WorkerApiKey);
        using var readerClient = app.CreateAuthenticatedClient(ReaderApiKey);
        using var coordinatorClient = app.CreateCoordinatorClient();
        using var foreignClient = app.CreateAuthenticatedClient(ForeignWorkerApiKey);

        var workerNoteResponse = await workerClient.PostAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/notes",
            new CreateNoteRequest(
                "## Context\nThe worker has started implementation.",
                "Context"));

        Assert.Equal(HttpStatusCode.Created, workerNoteResponse.StatusCode);

        var workerNote = await workerNoteResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(workerNote);
        Assert.Equal(task.Id, workerNote!.TaskId);
        Assert.Equal(worker.Id, workerNote.AgentId);
        Assert.Equal(worker.Name, workerNote.AgentName);
        Assert.Equal("worker", workerNote.AgentType);
        Assert.Equal("context", workerNote.NoteType);

        var coordinatorNoteResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/notes",
            new CreateNoteRequest(
                "Need a review pass once the endpoint contract is stable.",
                "ReviewRequest"));

        Assert.Equal(HttpStatusCode.Created, coordinatorNoteResponse.StatusCode);

        var coordinatorNote = await coordinatorNoteResponse.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(coordinatorNote);
        Assert.Equal("Coordinator", coordinatorNote!.AgentName);
        Assert.Equal("orchestrator", coordinatorNote.AgentType);
        Assert.Equal("reviewrequest", coordinatorNote.NoteType);

        var listedNotes = await readerClient.GetFromJsonAsync<List<NoteResponse>>(
            $"/api/v1/tasks/{task.Id}/notes");

        Assert.NotNull(listedNotes);
        Assert.Equal(2, listedNotes.Count);
        Assert.Equal(workerNote.Id, listedNotes[0].Id);
        Assert.Equal(coordinatorNote.Id, listedNotes[1].Id);
        Assert.True(listedNotes.SequenceEqual(listedNotes.OrderBy(note => note.CreatedAt), NoteIdComparer.Instance));

        var forbiddenResponse = await foreignClient.GetAsync($"/api/v1/tasks/{task.Id}/notes");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        var noteArtifacts = await app.QueryAsync(async db => new
        {
            NoteCount = await db.TaskNotes.CountAsync(note => note.TaskId == task.Id),
            NoteAddedEventCount = await db.TaskEvents.CountAsync(taskEvent =>
                taskEvent.TaskId == task.Id &&
                taskEvent.EventType == "note_added")
        });

        Assert.Equal(2, noteArtifacts.NoteCount);
        Assert.Equal(2, noteArtifacts.NoteAddedEventCount);
    }

    [Fact]
    public async Task DecisionRecords_CanBeCreatedListedFilteredAndFetched()
    {
        await using var app = new HiveboardApiFactory();

        var organization = IntegrationTestData.CreateDefaultOrganization();
        var foreignOrganization = IntegrationTestData.CreateOrganization("Foreign Decisions Org");
        var worker = IntegrationTestData.CreateAgent(organization.Id, "Decision Worker", AgentType.Worker, WorkerApiKey);
        var foreignWorker = IntegrationTestData.CreateAgent(
            foreignOrganization.Id,
            "Foreign Decision Worker",
            AgentType.Worker,
            ForeignWorkerApiKey);
        var project = IntegrationTestData.CreateProject(organization.Id, "Decision Project");
        var linkedTask = CreateTask(project.Id, "Implement endpoint notes");
        var unrelatedTask = CreateTask(project.Id, "Unrelated task");

        await app.SeedAsync(db =>
        {
            db.Organizations.AddRange(organization, foreignOrganization);
            db.Agents.AddRange(worker, foreignWorker);
            db.Projects.Add(project);
            db.AgentTasks.AddRange(linkedTask, unrelatedTask);
        });

        using var workerClient = app.CreateAuthenticatedClient(WorkerApiKey);
        using var coordinatorClient = app.CreateCoordinatorClient();
        using var foreignClient = app.CreateAuthenticatedClient(ForeignWorkerApiKey);

        var linkedDecisionResponse = await workerClient.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/decisions",
            new CreateDecisionRequest(
                "Keep note handling inside the API service layer",
                "## Rationale\n- Reuse current minimal API patterns\n- Keep free-form markdown untouched",
                linkedTask.Id,
                "Proposed"));

        Assert.Equal(HttpStatusCode.Created, linkedDecisionResponse.StatusCode);

        var linkedDecision = await linkedDecisionResponse.Content.ReadFromJsonAsync<DecisionResponse>();
        Assert.NotNull(linkedDecision);
        Assert.Equal(project.Id, linkedDecision!.ProjectId);
        Assert.Equal(linkedTask.Id, linkedDecision.TaskId);
        Assert.Equal("proposed", linkedDecision.Status);
        Assert.Contains("## Rationale", linkedDecision.Content);

        var standaloneDecisionResponse = await coordinatorClient.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/decisions",
            new CreateDecisionRequest(
                "Expose project decisions to all agents",
                "Accepted so workers can read architectural context directly.",
                null,
                "Accepted"));

        Assert.Equal(HttpStatusCode.Created, standaloneDecisionResponse.StatusCode);

        var standaloneDecision = await standaloneDecisionResponse.Content.ReadFromJsonAsync<DecisionResponse>();
        Assert.NotNull(standaloneDecision);
        Assert.Null(standaloneDecision!.TaskId);
        Assert.Equal("accepted", standaloneDecision.Status);
        Assert.Equal("Coordinator", standaloneDecision.AgentName);

        var acceptedTaskDecisionResponse = await workerClient.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/decisions",
            new CreateDecisionRequest(
                "Link implementation rationale to the task",
                "Accepted for the current task so reviewers can see the tradeoffs.",
                linkedTask.Id,
                "Accepted"));

        Assert.Equal(HttpStatusCode.Created, acceptedTaskDecisionResponse.StatusCode);

        var acceptedTaskDecision = await acceptedTaskDecisionResponse.Content.ReadFromJsonAsync<DecisionResponse>();
        Assert.NotNull(acceptedTaskDecision);
        Assert.Equal(linkedTask.Id, acceptedTaskDecision!.TaskId);
        Assert.Equal("accepted", acceptedTaskDecision.Status);

        var projectDecisions = await workerClient.GetFromJsonAsync<List<DecisionResponse>>(
            $"/api/v1/projects/{project.Id}/decisions");

        Assert.NotNull(projectDecisions);
        Assert.Equal(3, projectDecisions.Count);
        Assert.True(projectDecisions.SequenceEqual(
            projectDecisions.OrderByDescending(decision => decision.CreatedAt),
            DecisionIdComparer.Instance));

        var filteredDecisions = await workerClient.GetFromJsonAsync<List<DecisionResponse>>(
            $"/api/v1/projects/{project.Id}/decisions?status=Accepted&taskId={linkedTask.Id}");

        Assert.NotNull(filteredDecisions);
        Assert.Single(filteredDecisions);
        Assert.Equal(acceptedTaskDecision.Id, filteredDecisions[0].Id);

        var fetchedDecision = await workerClient.GetFromJsonAsync<DecisionResponse>(
            $"/api/v1/decisions/{linkedDecision.Id}");

        Assert.NotNull(fetchedDecision);
        Assert.Equal(linkedDecision.Id, fetchedDecision!.Id);
        Assert.Equal(worker.Name, fetchedDecision.AgentName);
        Assert.Equal("worker", fetchedDecision.AgentType);

        var foreignDecisionResponse = await foreignClient.GetAsync($"/api/v1/decisions/{linkedDecision.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, foreignDecisionResponse.StatusCode);
    }

    private static AgentTask CreateTask(Guid projectId, string title) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title,
            Description = string.Empty,
            Status = TaskStatusEnum.Backlog,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private sealed class NoteIdComparer : IEqualityComparer<NoteResponse>
    {
        public static NoteIdComparer Instance { get; } = new();

        public bool Equals(NoteResponse? x, NoteResponse? y) =>
            x?.Id == y?.Id;

        public int GetHashCode(NoteResponse obj) =>
            obj.Id.GetHashCode();
    }

    private sealed class DecisionIdComparer : IEqualityComparer<DecisionResponse>
    {
        public static DecisionIdComparer Instance { get; } = new();

        public bool Equals(DecisionResponse? x, DecisionResponse? y) =>
            x?.Id == y?.Id;

        public int GetHashCode(DecisionResponse obj) =>
            obj.Id.GetHashCode();
    }
}
