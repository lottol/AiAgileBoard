namespace AiAgileBoard.Domain;

public sealed class TicketComment
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public required string Body { get; set; }
}
