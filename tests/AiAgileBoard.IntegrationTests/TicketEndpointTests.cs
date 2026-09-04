using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace AiAgileBoard.IntegrationTests;

public sealed class TicketEndpointTests : IClassFixture<AiAgileBoardWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TicketEndpointTests(AiAgileBoardWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SubmitTicketPersistsAndReturnsTicket()
    {
        var request = new
        {
            title = "Create the ticket backend",
            description = "Persist the initial ticket fields.",
            comments = new[] { "First comment" },
            storyPoints = 3,
            state = "Backlog",
            assignee = "Agent"
        };

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/tickets",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEqual(Guid.Empty, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Backlog", body.RootElement.GetProperty("state").GetString());
        Assert.True(body.RootElement.GetProperty("humanNeeded").GetBoolean());
        Assert.Equal("Agent", body.RootElement.GetProperty("assignee").GetString());
        Assert.Equal(
            "First comment",
            body.RootElement.GetProperty("comments")[0].GetString());
    }

    [Fact]
    public async Task SubmitTicketRejectsUnknownState()
    {
        var request = new
        {
            title = "Invalid ticket",
            description = "This state does not exist.",
            comments = Array.Empty<string>(),
            storyPoints = 1,
            state = "Imaginary",
            assignee = "Human"
        };

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/tickets",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
