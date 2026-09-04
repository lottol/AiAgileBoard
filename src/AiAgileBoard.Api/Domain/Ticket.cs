using System.Text.Json.Serialization;

namespace AiAgileBoard.Domain;

public sealed class Ticket
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }

    public int StoryPoints { get; set; }

    public Assignee Assignee { get; set; }

    public int StateId { get; set; }

    public State State { get; set; } = null!;

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public ICollection<TicketComment> Comments { get; } = [];
}
