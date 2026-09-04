using AiAgileBoard.Application;
using AiAgileBoard.Domain;
using Microsoft.EntityFrameworkCore;

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
        CancellationToken cancellationToken,
        string? state = null,
        Assignee? assignee = null,
        bool? humanNeeded = null,
        int? minStoryPoints = null,
        int? maxStoryPoints = null,
        string? search = null,
        int skip = 0,
        int take = 100)
    {
        if (skip < 0 || take is < 1 or > 500)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pagination"] = ["Skip cannot be negative and take must be between 1 and 500."]
            });
        }

        if (minStoryPoints is not null &&
            maxStoryPoints is not null &&
            minStoryPoints.Value > maxStoryPoints.Value)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["storyPoints"] = ["Minimum story points cannot exceed maximum story points."]
            });
        }

        var tickets = ticketService.QueryTickets();

        if (!string.IsNullOrWhiteSpace(state))
        {
            var stateName = state.Trim();
            tickets = tickets.Where(ticket => ticket.State.Name == stateName);
        }

        if (assignee is not null)
        {
            var assigneeValue = assignee.Value;
            tickets = tickets.Where(ticket => ticket.Assignee == assigneeValue);
        }

        if (humanNeeded is not null)
        {
            var humanNeededValue = humanNeeded.Value;
            tickets = tickets.Where(ticket => ticket.State.HumanNeeded == humanNeededValue);
        }

        if (minStoryPoints is not null)
        {
            var minimum = minStoryPoints.Value;
            tickets = tickets.Where(ticket => ticket.StoryPoints >= minimum);
        }

        if (maxStoryPoints is not null)
        {
            var maximum = maxStoryPoints.Value;
            tickets = tickets.Where(ticket => ticket.StoryPoints <= maximum);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            tickets = tickets.Where(ticket =>
                ticket.Title.Contains(searchTerm) || ticket.Description.Contains(searchTerm));
        }

        var results = await tickets
            .OrderBy(ticket => ticket.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

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
