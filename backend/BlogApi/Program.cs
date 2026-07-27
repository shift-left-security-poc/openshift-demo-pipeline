using BlogApi.Auth;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/posts"),
    appBuilder => appBuilder.UseMiddleware<ApiKeyMiddleware>());

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/posts", () => Results.Ok(Array.Empty<object>()));

app.Run();

public partial class Program { }
