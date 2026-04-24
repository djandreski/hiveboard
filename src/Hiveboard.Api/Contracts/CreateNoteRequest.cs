namespace Hiveboard.Api.Contracts;

public record CreateNoteRequest(
    string? Content,
    string? NoteType);
