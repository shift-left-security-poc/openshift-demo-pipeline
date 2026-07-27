using BlogApi.Data;
using BlogApi.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlogApi.Tests;

public class BlogDbContextTests
{
    private static BlogDbContext CreateInMemoryContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;
        return new BlogDbContext(options);
    }

    [Fact]
    public async Task CanAddAndRetrievePost()
    {
        var databaseName = System.Guid.NewGuid().ToString();
        var post = new Post
        {
            Id = System.Guid.NewGuid(),
            Title = "Hello",
            Content = "World",
            Author = "Alex",
            IsPublished = true,
            CreatedAtUtc = System.DateTime.UtcNow,
            UpdatedAtUtc = System.DateTime.UtcNow
        };

        await using (var context = CreateInMemoryContext(databaseName))
        {
            context.Posts.Add(post);
            await context.SaveChangesAsync();
        }

        await using var verificationContext = CreateInMemoryContext(databaseName);
        var retrieved = await verificationContext.Posts.FindAsync(post.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(post.Id, retrieved!.Id);
        Assert.Equal("Hello", retrieved!.Title);
        Assert.Equal("World", retrieved.Content);
        Assert.Equal("Alex", retrieved.Author);
        Assert.True(retrieved.IsPublished);
        Assert.Equal(post.CreatedAtUtc, retrieved.CreatedAtUtc);
        Assert.Equal(post.UpdatedAtUtc, retrieved.UpdatedAtUtc);
    }
}
