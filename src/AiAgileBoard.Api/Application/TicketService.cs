using AiAgileBoard.Data;
using AiAgileBoard.Domain;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AiAgileBoard.Application;

public sealed class TicketService(AgileBoardDbContext dbContext)
{
    public Task<List<Ticket>> QueryTicketsAsync(
        Func<IQueryable<Ticket>, IQueryable<Ticket>> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tickets = dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.State)
            .Include(ticket => ticket.Comments);

        return query(tickets).ToListAsync(cancellationToken);
    }

    public async Task<Ticket> SubmitTicketAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
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

        State? state;
        if (ticket.StateId > 0)
        {
            state = await dbContext.States.SingleOrDefaultAsync(
                item => item.Id == ticket.StateId,
                cancellationToken);
        }
        else
        {
            var stateName = string.IsNullOrWhiteSpace(ticket.State?.Name)
                ? "Backlog"
                : ticket.State.Name.Trim();
            state = await dbContext.States.SingleOrDefaultAsync(
                item => item.Name == stateName,
                cancellationToken);
        }

        if (state is null)
        {
            var requestedState = ticket.StateId > 0
                ? ticket.StateId.ToString(CultureInfo.InvariantCulture)
                : ticket.State?.Name ?? "Backlog";
            throw new ArgumentException(
                $"The state '{requestedState}' does not exist.",
                nameof(ticket));
        }

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
}
