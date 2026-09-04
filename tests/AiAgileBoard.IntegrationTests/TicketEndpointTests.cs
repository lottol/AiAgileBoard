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
            comments = new[] { new { body = "First comment" } },
            storyPoints = 3,
            state = new { name = "Backlog" },
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
            comments = Array.Empty<object>(),
            storyPoints = 1,
            state = new { name = "Imaginary" },
            assignee = "Human"
        };

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/tickets",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QueryTicketsAppliesFilters()
    {
        var request = new
        {
            title = "Queryable ticket",
            description = "Find this ticket through the API.",
            comments = Array.Empty<object>(),
            storyPoints = 8,
            state = new { name = "Waiting for Agent" },
            assignee = "Agent"
        };

        using var submitResponse = await _client.PostAsJsonAsync(
            "/api/v1/tickets",
            request,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);

        using var queryResponse = await _client.GetAsync(
            "/api/v1/tickets?state=Waiting%20for%20Agent&assignee=Agent&minStoryPoints=5&search=Queryable",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await queryResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        var ticket = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal("Queryable ticket", ticket.GetProperty("title").GetString());
        Assert.Equal("Waiting for Agent", ticket.GetProperty("state").GetString());
    }
}
