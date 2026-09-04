namespace AiAgileBoard.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<AiAgileBoardWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(AiAgileBoardWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpointReturnsSuccess()
    {
        using var response = await _client.GetAsync(
            "/api/v1/health",
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
