namespace AiAgileBoard.Domain;

public sealed class State
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public bool HumanNeeded { get; set; }

    public ICollection<Ticket> Tickets { get; } = [];
}
