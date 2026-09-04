using AiAgileBoard.Domain;

namespace AiAgileBoard.Application;

public sealed record SubmitTicketRequest(
    string Title,
    string Description,
    IReadOnlyCollection<string>? Comments,
    int StoryPoints,
    string? State,
    Assignee Assignee);
