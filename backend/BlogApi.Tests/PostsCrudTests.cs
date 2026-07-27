using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlogApi.Tests;

public class PostsCrudTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PostsCrudTests(WebApplicationFactory<Program> factory)
    {
        var configuredFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApiKey"] = "test-key-123"
                });
            });
        });
        _client = configuredFactory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", "test-key-123");
    }

    [Fact]
    public async Task CreateThenGetPost_ReturnsCreatedPost()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/posts", new
        {
            title = "First Post",
            content = "Content here",
            author = "Alex"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(created);

        var getResponse = await _client.GetAsync($"/api/posts/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetNonExistentPost_Returns404()
    {
        var response = await _client.GetAsync($"/api/posts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatePost_WithMissingTitle_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/posts", new
        {
            title = "",
            content = "Content",
            author = "Alex"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private record PostResponse(Guid Id, string Title, string Content, string Author);
}
