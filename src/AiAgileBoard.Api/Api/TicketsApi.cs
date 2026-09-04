using AiAgileBoard.Application;
using AiAgileBoard.Domain;

namespace AiAgileBoard.Api;

public static class TicketsApi
{
    public static RouteGroupBuilder MapTicketEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/tickets", QueryTicketsAsync)
            .WithName("QueryTickets");

        api.MapGet("/tickets/{ticketId:guid}", GetTicketAsync)
            .WithName("GetTicket");

        api.MapPost("/tickets", SubmitTicketAsync)
            .WithName("SubmitTicket");

        api.MapPut("/tickets/{ticketId:guid}", UpdateTicketAsync)
            .WithName("UpdateTicket");

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

    private static async Task<IResult> GetTicketAsync(
        Guid ticketId,
        TicketService ticketService,
        CancellationToken cancellationToken)
    {
        var ticket = await ticketService.GetTicketAsync(ticketId, cancellationToken);
        return ticket is null ? Results.NotFound() : Results.Ok(ToResponse(ticket));
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

    private static async Task<IResult> UpdateTicketAsync(
        Guid ticketId,
        TicketUpdateRequest request,
        TicketService ticketService,
        CancellationToken cancellationToken)
    {
        try
        {
            var ticket = await ticketService.UpdateTicketAsync(
                ticketId,
                new Ticket
                {
                    Title = request.Title,
                    Description = request.Description,
                    StoryPoints = request.StoryPoints,
                    Assignee = request.Assignee,
                    State = new State { Name = request.State }
                },
                cancellationToken);

            return ticket is null ? Results.NotFound() : Results.Ok(ToResponse(ticket));
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
        IReadOnlyCollection<string> Comments,
        int StoryPoints,
        string State,
        bool HumanNeeded,
        Assignee Assignee);

    private sealed record TicketUpdateRequest(
        string Title,
        string Description,
        int StoryPoints,
        string State,
        Assignee Assignee);
}
