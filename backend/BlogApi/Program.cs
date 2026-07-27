using BlogApi.Auth;
using BlogApi.Data;
using BlogApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BlogDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("BlogDb");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseInMemoryDatabase("BlogDbFallback");
    }
});

var app = builder.Build();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/posts"),
    appBuilder => appBuilder.UseMiddleware<ApiKeyMiddleware>());

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

var posts = app.MapGroup("/api/posts");

posts.MapGet("", async (BlogDbContext db) =>
    Results.Ok(await db.Posts.ToListAsync()));

posts.MapGet("/{id:guid}", async (Guid id, BlogDbContext db) =>
{
    var post = await db.Posts.FindAsync(id);
    return post is null ? Results.NotFound() : Results.Ok(post);
});

posts.MapPost("", async (CreatePostRequest request, BlogDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) ||
        string.IsNullOrWhiteSpace(request.Content) ||
        string.IsNullOrWhiteSpace(request.Author))
    {
        return Results.BadRequest(new { error = "Title, content, and author are required" });
    }

    var post = new Post
    {
        Id = Guid.NewGuid(),
        Title = request.Title,
        Content = request.Content,
        Author = request.Author,
        IsPublished = request.IsPublished,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    db.Posts.Add(post);
    await db.SaveChangesAsync();

    return Results.Created($"/api/posts/{post.Id}", post);
});

posts.MapPut("/{id:guid}", async (Guid id, UpdatePostRequest request, BlogDbContext db) =>
{
    var post = await db.Posts.FindAsync(id);
    if (post is null)
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(request.Title) ||
        string.IsNullOrWhiteSpace(request.Content) ||
        string.IsNullOrWhiteSpace(request.Author))
    {
        return Results.BadRequest(new { error = "Title, content, and author are required" });
    }

    post.Title = request.Title;
    post.Content = request.Content;
    post.Author = request.Author;
    post.IsPublished = request.IsPublished;
    post.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(post);
});

posts.MapDelete("/{id:guid}", async (Guid id, BlogDbContext db) =>
{
    var post = await db.Posts.FindAsync(id);
    if (post is null)
    {
        return Results.NotFound();
    }

    db.Posts.Remove(post);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

public partial class Program { }

record CreatePostRequest(string Title, string Content, string Author, bool IsPublished = false);
record UpdatePostRequest(string Title, string Content, string Author, bool IsPublished);
