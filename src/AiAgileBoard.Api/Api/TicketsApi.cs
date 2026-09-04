using AiAgileBoard.Application;
using AiAgileBoard.Domain;

namespace AiAgileBoard.Api;

public static class TicketsApi
{
    public static RouteGroupBuilder MapTicketEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/tickets", QueryTicketsAsync)
            .WithName("QueryTickets");

        api.MapPost("/tickets", SubmitTicketAsync)
            .WithName("SubmitTicket");

        return api;
    }

    private static async Task<IResult> QueryTicketsAsync(
        TicketService ticketService,
        CancellationToken cancellationToken)
    {
        var results = await ticketService.QueryTicketsAsync(
            tickets => tickets.OrderBy(ticket => ticket.Id),
            cancellationToken);

        return Results.Ok(results.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> SubmitTicketAsync(
        Ticket ticket,
        TicketService ticketService,
        CancellationToken cancellationToken)
    {
        try
        {
            var submittedTicket = await ticketService.SubmitTicketAsync(ticket, cancellationToken);
            return Results.Created(
                $"/api/v1/tickets/{submittedTicket.Id}",
                ToResponse(submittedTicket));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ticket"] = [exception.Message]
            });
        }
    }

    private static TicketResponse ToResponse(Ticket ticket)
    {
        return new TicketResponse(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Comments.Select(comment => comment.Body).ToArray(),
            ticket.StoryPoints,
            ticket.State.Name,
            ticket.State.HumanNeeded,
            ticket.Assignee);
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
