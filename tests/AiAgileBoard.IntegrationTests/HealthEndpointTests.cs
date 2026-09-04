using Microsoft.AspNetCore.Mvc.Testing;

namespace AiAgileBoard.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
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
