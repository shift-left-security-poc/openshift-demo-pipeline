namespace BlogApi.Auth;

public class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var expectedKey = _configuration["ApiKey"];

        if (string.IsNullOrEmpty(expectedKey) ||
            !context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
            providedKey != expectedKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing API key" });
            return;
        }

        await _next(context);
    }
}
