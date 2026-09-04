using AiAgileBoard.Data;
using AiAgileBoard.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiAgileBoard.Application;

public sealed class TicketService(AgileBoardDbContext dbContext)
{
    public async Task<Ticket> SubmitTicketAsync(
        SubmitTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Description);

        if (request.StoryPoints < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Story points cannot be negative.");
        }

        var stateName = string.IsNullOrWhiteSpace(request.State) ? "Backlog" : request.State.Trim();
        var state = await dbContext.States.SingleOrDefaultAsync(
            item => item.Name == stateName,
            cancellationToken);

        if (state is null)
        {
            throw new ArgumentException($"The state '{stateName}' does not exist.", nameof(request));
        }

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            StoryPoints = request.StoryPoints,
            Assignee = request.Assignee,
            StateId = state.Id,
            State = state
        };

        foreach (var body in request.Comments ?? [])
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                ticket.Comments.Add(new TicketComment
                {
                    Id = Guid.NewGuid(),
                    Body = body.Trim(),
                    Ticket = ticket,
                    TicketId = ticket.Id
                });
            }
        }

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }
}
