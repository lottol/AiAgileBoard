using AiAgileBoard.Data;
using AiAgileBoard.Domain;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AiAgileBoard.Application;

public sealed class TicketService(AgileBoardDbContext dbContext)
{
    public async Task<Ticket?> GetTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.State)
            .Include(ticket => ticket.Comments)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
    }

    public async Task<IReadOnlyList<Ticket>> QueryTicketsAsync(
        Func<IQueryable<Ticket>, IQueryable<Ticket>> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tickets = dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.State)
            .Include(ticket => ticket.Comments);

        var composedQuery = query(tickets) ?? throw new ArgumentException(
            "The LINQ query must return a ticket query.",
            nameof(query));

        return await composedQuery.ToListAsync(cancellationToken);
    }

    public async Task<Ticket> SubmitTicketAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ValidateEditableFields(ticket);

        var state = await ResolveStateAsync(ticket, cancellationToken);

        ticket.Id = Guid.NewGuid();
        ticket.Title = ticket.Title.Trim();
        ticket.Description = ticket.Description.Trim();
        ticket.StateId = state.Id;
        ticket.State = state;

        foreach (var comment in ticket.Comments.ToArray())
        {
            if (string.IsNullOrWhiteSpace(comment.Body))
            {
                ticket.Comments.Remove(comment);
                continue;
            }

            comment.Id = Guid.NewGuid();
            comment.Body = comment.Body.Trim();
            comment.Ticket = ticket;
            comment.TicketId = ticket.Id;
        }

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<Ticket?> UpdateTicketAsync(
        Guid ticketId,
        Ticket changes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var ticket = await dbContext.Tickets
            .Include(item => item.State)
            .Include(item => item.Comments)
            .SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        ValidateEditableFields(changes);
        var state = await ResolveStateAsync(changes, cancellationToken);

        ticket.Title = changes.Title.Trim();
        ticket.Description = changes.Description.Trim();
        ticket.StoryPoints = changes.StoryPoints;
        ticket.Assignee = changes.Assignee;
        ticket.StateId = state.Id;
        ticket.State = state;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    private static void ValidateEditableFields(Ticket ticket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticket.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(ticket.Description);

        if (ticket.StoryPoints < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticket),
                "Story points cannot be negative.");
        }

        if (!Enum.IsDefined(ticket.Assignee))
        {
            throw new ArgumentException("Assignee must be Human or Agent.", nameof(ticket));
        }
    }

    private async Task<State> ResolveStateAsync(
        Ticket ticket,
        CancellationToken cancellationToken)
    {
        if (ticket.StateId < 0)
        {
            throw new ArgumentException("State ID cannot be negative.", nameof(ticket));
        }

        var requestedStateName = string.IsNullOrWhiteSpace(ticket.State?.Name)
            ? null
            : ticket.State.Name.Trim();

        State? state;
        if (ticket.StateId > 0)
        {
            state = await dbContext.States.SingleOrDefaultAsync(
                item => item.Id == ticket.StateId,
                cancellationToken);

            if (state is not null &&
                requestedStateName is not null &&
                !string.Equals(state.Name, requestedStateName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "State ID and state name must identify the same state.",
                    nameof(ticket));
            }
        }
        else
        {
            var stateName = requestedStateName ?? "Backlog";
            state = await dbContext.States.SingleOrDefaultAsync(
                item => item.Name == stateName,
                cancellationToken);
        }

        if (state is not null)
        {
            return state;
        }

        var requestedState = ticket.StateId > 0
            ? ticket.StateId.ToString(CultureInfo.InvariantCulture)
            : requestedStateName ?? "Backlog";
        throw new ArgumentException(
            $"The state '{requestedState}' does not exist.",
            nameof(ticket));
    }
}
