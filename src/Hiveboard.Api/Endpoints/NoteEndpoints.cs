using Hiveboard.Api.Application;
using Hiveboard.Api.Contracts;

namespace Hiveboard.Api.Endpoints;

public static class NoteEndpoints
{
    public static void MapTaskNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Notes")
            .RequireAuthorization();

        group.MapPost("/tasks/{id:guid}/notes", CreateTaskNote)
            .WithName("CreateTaskNote")
            .WithSummary("Add a note to a task")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Creates a task note attributed to the authenticated caller and records a note_added task event.")
            .Produces<NoteResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/tasks/{id:guid}/notes", ListTaskNotes)
            .WithName("ListTaskNotes")
            .WithSummary("List notes for a task")
            .WithDescription("Auth: Coordinator/admin key or any agent API key. Returns all notes for a task in creation order, including the author name and type.")
            .Produces<List<NoteResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static Task<IResult> CreateTaskNote(
        Guid id,
        CreateNoteRequest? request,
        NotesAndDecisionsApplicationService applicationService) =>
        applicationService.CreateTaskNoteAsync(id, request);

    private static Task<IResult> ListTaskNotes(
        Guid id,
        NotesAndDecisionsApplicationService applicationService) =>
        applicationService.ListTaskNotesAsync(id);
}
