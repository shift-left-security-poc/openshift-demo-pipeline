using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BlogApi.Tests;

public class ApiKeyMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiKeyMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApiKey"] = "test-key-123"
                });
            });
        });
    }

    [Fact]
    public async Task Posts_WithoutApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/posts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Posts_WithValidApiKey_ReturnsOk()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "test-key-123");

        var response = await client.GetAsync("/api/posts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
