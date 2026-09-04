using AiAgileBoard.Application;
using AiAgileBoard.Domain;

namespace AiAgileBoard.Api;

public static class TicketsApi
{
    public static RouteGroupBuilder MapTicketEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/tickets", SubmitTicketAsync)
            .WithName("SubmitTicket");

        return api;
    }

    private static async Task<IResult> SubmitTicketAsync(
        SubmitTicketRequest request,
        TicketService ticketService,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Assignee))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Assignee)] = ["Assignee must be Human or Agent."]
            });
        }

        try
        {
            var ticket = await ticketService.SubmitTicketAsync(request, cancellationToken);
            return Results.Created($"/api/v1/tickets/{ticket.Id}", new TicketResponse(
                ticket.Id,
                ticket.Title,
                ticket.Description,
                ticket.Comments.Select(comment => comment.Body),
                ticket.StoryPoints,
                ticket.State.Name,
                ticket.State.HumanNeeded,
                ticket.Assignee));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ticket"] = [exception.Message]
            });
        }
    }

    private sealed record TicketResponse(
        Guid Id,
        string Title,
        string Description,
        IEnumerable<string> Comments,
        int StoryPoints,
        string State,
        bool HumanNeeded,
        Assignee Assignee);
}
