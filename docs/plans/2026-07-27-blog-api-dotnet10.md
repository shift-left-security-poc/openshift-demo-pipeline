# .NET 10 Blog API on OpenShift Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a .NET 10 minimal API backend for blog posts, backed by PostgreSQL, secured with a static API key, and deployable to OpenShift via Helm alongside the existing `gremlins` app.

**Architecture:** A new `backend/BlogApi` .NET 10 minimal API project exposes `/health` (unauthenticated) and `/api/posts` CRUD endpoints (protected by an `X-API-Key` header). EF Core + Npgsql persists `Post` entities to a PostgreSQL instance deployed in the `devops` OpenShift namespace. A new `helm/blogapi` chart deploys the API (DeploymentConfig/Service/Route/Secret) and a `helm/postgres` chart deploys PostgreSQL (Deployment/Service/PVC/Secret). A `backend/Dockerfile` builds the API image for an OpenShift Docker-strategy BuildConfig + ImageStream, following the same pattern as the existing `gremlins` app.

**Tech Stack:** .NET 10 (minimal API), EF Core + Npgsql, PostgreSQL, xUnit + WebApplicationFactory for tests, Helm, OpenShift (BuildConfig/ImageStream/DeploymentConfig/Service/Route/Secret/PVC).

---

## Reference Conventions From This Repo

- Existing app `gremlins` uses: `DOCKERFILE` (root) → S2I/Docker build → `openshift/imagestream.yaml` (ImageStream) → Helm chart in `helm/gremlins/` with `deploymentconfig.yaml`, `service.yaml`, `route.yaml`, `values.yaml`, `_helpers.tpl`.
- Namespace used throughout is `devops`.
- Service ports pattern: expose `8080-tcp` (and `8443-tcp` for gremlins, not needed here).
- `dotnet --version` on this machine reports `10.0.105`, confirming .NET 10 SDK is available locally for `dotnet test`/`dotnet build`.

---

## Task 1: Scaffold the .NET 10 Blog API project

**Files:**
- Create: `backend/BlogApi/BlogApi.csproj`
- Create: `backend/BlogApi/Program.cs`
- Create: `backend/BlogApi.sln`

**Step 1: Create the solution and project**

Run:
```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
mkdir -p backend/BlogApi
cd backend
dotnet new sln -n BlogApi
cd BlogApi
dotnet new web -n BlogApi -o .
cd ..
dotnet sln add BlogApi/BlogApi.csproj
```

**Step 2: Add required NuGet packages to `backend/BlogApi/BlogApi.csproj`**

Run:
```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline/backend/BlogApi
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
```

**Step 3: Verify it builds**

Run: `dotnet build backend/BlogApi.sln`
Expected: `Build succeeded.`

**Step 4: Commit**

```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
git add backend/
git commit -m "Scaffold BlogApi .NET 10 minimal API project"
```

---

## Task 2: Add the test project (xUnit + WebApplicationFactory)

**Files:**
- Create: `backend/BlogApi.Tests/BlogApi.Tests.csproj`
- Create: `backend/BlogApi.Tests/HealthEndpointTests.cs`

**Step 1: Scaffold test project**

Run:
```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline/backend
dotnet new xunit -n BlogApi.Tests -o BlogApi.Tests
dotnet add BlogApi.Tests/BlogApi.Tests.csproj reference BlogApi/BlogApi.csproj
dotnet add BlogApi.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet sln add BlogApi.Tests/BlogApi.Tests.csproj
```

**Step 2: Write the failing test** (`backend/BlogApi.Tests/HealthEndpointTests.cs`)

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BlogApi.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsOk_WithoutApiKey()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

**Step 3: Run test to verify it fails**

Run: `dotnet test backend/BlogApi.sln --filter Health_ReturnsOk_WithoutApiKey`
Expected: FAIL (compile error: `Program` not accessible, or 404 - `/health` not defined yet)

**Step 4: Make `Program` accessible to the test project.** In `backend/BlogApi/Program.cs`, at the very end of the file, add:

```csharp
public partial class Program { }
```

**Step 5: Implement minimal `/health` endpoint in `backend/BlogApi/Program.cs`**

Replace the generated template content with:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program { }
```

**Step 6: Run test to verify it passes**

Run: `dotnet test backend/BlogApi.sln --filter Health_ReturnsOk_WithoutApiKey`
Expected: PASS

**Step 7: Commit**

```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
git add backend/
git commit -m "Add BlogApi health endpoint with failing-first test"
```

---

## Task 3: Define the `Post` entity and EF Core `DbContext`

**Files:**
- Create: `backend/BlogApi/Models/Post.cs`
- Create: `backend/BlogApi/Data/BlogDbContext.cs`
- Test: `backend/BlogApi.Tests/BlogDbContextTests.cs`

**Step 1: Write the failing test** (`backend/BlogApi.Tests/BlogDbContextTests.cs`)

```csharp
using BlogApi.Data;
using BlogApi.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlogApi.Tests;

public class BlogDbContextTests
{
    private static BlogDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
            .Options;
        return new BlogDbContext(options);
    }

    [Fact]
    public async Task CanAddAndRetrievePost()
    {
        await using var context = CreateInMemoryContext();
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

        context.Posts.Add(post);
        await context.SaveChangesAsync();

        var retrieved = await context.Posts.FindAsync(post.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Hello", retrieved!.Title);
    }
}
```

**Step 2: Add EF Core InMemory package to test project**

Run:
```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline/backend
dotnet add BlogApi.Tests package Microsoft.EntityFrameworkCore.InMemory
```

**Step 3: Run test to verify it fails**

Run: `dotnet test backend/BlogApi.sln --filter CanAddAndRetrievePost`
Expected: FAIL (compile error: `BlogApi.Models.Post`, `BlogApi.Data.BlogDbContext` not found)

**Step 4: Implement `Post` entity** (`backend/BlogApi/Models/Post.cs`)

```csharp
namespace BlogApi.Models;

public class Post
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```

**Step 5: Implement `BlogDbContext`** (`backend/BlogApi/Data/BlogDbContext.cs`)

```csharp
using BlogApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogApi.Data;

public class BlogDbContext : DbContext
{
    public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options)
    {
    }

    public DbSet<Post> Posts => Set<Post>();
}
```

**Step 6: Run test to verify it passes**

Run: `dotnet test backend/BlogApi.sln --filter CanAddAndRetrievePost`
Expected: PASS

**Step 7: Commit**

```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
git add backend/
git commit -m "Add Post entity and BlogDbContext with in-memory test"
```

---

## Task 4: Add API-key authentication middleware

**Files:**
- Create: `backend/BlogApi/Auth/ApiKeyMiddleware.cs`
- Modify: `backend/BlogApi/Program.cs`
- Test: `backend/BlogApi.Tests/ApiKeyMiddlewareTests.cs`

**Step 1: Write the failing test** (`backend/BlogApi.Tests/ApiKeyMiddlewareTests.cs`)

```csharp
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test backend/BlogApi.sln --filter ApiKeyMiddlewareTests`
Expected: FAIL (404 for `/api/posts` — endpoint doesn't exist yet)

**Step 3: Implement the middleware** (`backend/BlogApi/Auth/ApiKeyMiddleware.cs`)

```csharp
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
```

**Step 4: Wire up middleware and a placeholder `/api/posts` GET endpoint in `backend/BlogApi/Program.cs`**

```csharp
using BlogApi.Auth;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

var posts = app.MapGroup("/api/posts");
posts.Use(async (context, next) =>
{
    var middleware = new ApiKeyMiddleware(next.Invoke, context.RequestServices.GetRequiredService<IConfiguration>());
    await middleware.InvokeAsync(context);
});
posts.MapGet("/", () => Results.Ok(Array.Empty<object>()));

app.Run();

public partial class Program { }
```

Note: `MapGroup().Use()` requires wrapping `RequestDelegate` correctly — if this pattern causes issues, use `app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api/posts"), appBuilder => appBuilder.UseMiddleware<ApiKeyMiddleware>());` placed before `MapGroup` registration instead. Verify with the tests in Step 5.

**Step 5: Run tests to verify they pass**

Run: `dotnet test backend/BlogApi.sln --filter ApiKeyMiddlewareTests`
Expected: PASS (both tests)

**Step 6: Run full test suite to confirm no regressions**

Run: `dotnet test backend/BlogApi.sln`
Expected: All tests pass.

**Step 7: Commit**

```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
git add backend/
git commit -m "Add API key middleware protecting /api/posts"
```

---

## Task 5: Implement full CRUD endpoints for posts

**Files:**
- Modify: `backend/BlogApi/Program.cs`
- Test: `backend/BlogApi.Tests/PostsCrudTests.cs`

**Step 1: Write failing tests** (`backend/BlogApi.Tests/PostsCrudTests.cs`)

```csharp
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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test backend/BlogApi.sln --filter PostsCrudTests`
Expected: FAIL (placeholder GET returns empty array/404 for all; POST/PUT/DELETE undefined)

**Step 3: Implement CRUD endpoints backed by EF Core InMemory (wired to DbContext) in `backend/BlogApi/Program.cs`**

Replace the full file with:

```csharp
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

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

var posts = app.MapGroup("/api/posts");
posts.Use(async (context, next) =>
{
    var middleware = new ApiKeyMiddleware(next.Invoke, context.RequestServices.GetRequiredService<IConfiguration>());
    await middleware.InvokeAsync(context);
});

posts.MapGet("/", async (BlogDbContext db) =>
    Results.Ok(await db.Posts.ToListAsync()));

posts.MapGet("/{id:guid}", async (Guid id, BlogDbContext db) =>
{
    var post = await db.Posts.FindAsync(id);
    return post is null ? Results.NotFound() : Results.Ok(post);
});

posts.MapPost("/", async (CreatePostRequest request, BlogDbContext db) =>
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
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test backend/BlogApi.sln --filter PostsCrudTests`
Expected: PASS

**Step 5: Run full test suite**

Run: `dotnet test backend/BlogApi.sln`
Expected: All tests pass, no regressions.

**Step 6: Commit**

```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
git add backend/
git commit -m "Implement full CRUD endpoints for blog posts"
```

---

## Task 6: Add EF Core migrations and PostgreSQL startup migration

**Files:**
- Create: `backend/BlogApi/Migrations/*` (generated)
- Modify: `backend/BlogApi/Program.cs`

**Step 1: Generate the initial migration**

Run:
```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline/backend/BlogApi
dotnet tool install --global dotnet-ef --version 10.* 2>/dev/null || true
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add InitialCreate --project . --startup-project .
```
Expected: `Migrations` folder created with `InitialCreate` migration files.

Note: If `dotnet ef` requires a real Npgsql connection string at design time and fails, temporarily set `ConnectionStrings:BlogDb` to a placeholder Postgres string via `DOTNET_ENVIRONMENT` or a design-time factory; do not commit real credentials.

**Step 2: Apply migrations automatically at startup.** In `backend/BlogApi/Program.cs`, immediately after `var app = builder.Build();`, add:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    if (app.Configuration.GetConnectionString("BlogDb") is not null)
    {
        db.Database.Migrate();
    }
}
```

**Step 3: Run full test suite to confirm no regressions** (tests use in-memory provider, so `Database.Migrate()` path is skipped since no connection string is configured in tests)

Run: `dotnet test backend/BlogApi.sln`
Expected: All tests pass.

**Step 4: Commit**

```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
git add backend/
git commit -m "Add EF Core migrations and startup auto-migration for PostgreSQL"
```

---

## Task 7: Add backend Dockerfile

**Files:**
- Create: `backend/Dockerfile`

**Step 1: Write the Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BlogApi/BlogApi.csproj BlogApi/
RUN dotnet restore BlogApi/BlogApi.csproj
COPY BlogApi/ BlogApi/
RUN dotnet publish BlogApi/BlogApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BlogApi.dll"]
```

**Step 2: Verify the image builds locally**

Run:
```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline/backend
docker build -t blogapi:local . 2>&1 || podman build -t blogapi:local .
```
Expected: Image builds successfully. (If neither `docker` nor `podman` is available locally, skip this local verification and rely on OpenShift BuildConfig verification in Task 9.)

**Step 3: Commit**

```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
git add backend/Dockerfile
git commit -m "Add backend Dockerfile for BlogApi"
```

---

## Task 8: Add PostgreSQL Helm chart

**Files:**
- Create: `helm/postgres/Chart.yaml`
- Create: `helm/postgres/values.yaml`
- Create: `helm/postgres/templates/deployment.yaml`
- Create: `helm/postgres/templates/service.yaml`
- Create: `helm/postgres/templates/pvc.yaml`
- Create: `helm/postgres/templates/secret.yaml`

**Step 1: `helm/postgres/Chart.yaml`**

```yaml
apiVersion: v2
name: postgres
description: PostgreSQL database for BlogApi
type: application
version: 0.1.0
appVersion: "16"
```

**Step 2: `helm/postgres/values.yaml`**

```yaml
nameOverride: blogapi-postgres
namespace: devops
image:
  repository: registry.redhat.io/rhel9/postgresql-16
  tag: latest
storage:
  size: 1Gi
credentials:
  database: blogdb
  username: blogapi
  # password is intentionally left blank here; set via --set or a separate
  # values-secret.yaml (gitignored) when running `helm install`/`upgrade`.
  password: ""
```

**Step 3: `helm/postgres/templates/secret.yaml`**

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: {{ .Values.nameOverride }}-credentials
  namespace: {{ .Values.namespace }}
type: Opaque
stringData:
  POSTGRES_DB: {{ .Values.credentials.database }}
  POSTGRES_USER: {{ .Values.credentials.username }}
  POSTGRES_PASSWORD: {{ .Values.credentials.password | quote }}
```

**Step 4: `helm/postgres/templates/pvc.yaml`**

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: {{ .Values.nameOverride }}-data
  namespace: {{ .Values.namespace }}
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: {{ .Values.storage.size }}
```

**Step 5: `helm/postgres/templates/deployment.yaml`**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ .Values.nameOverride }}
  namespace: {{ .Values.namespace }}
  labels:
    app: {{ .Values.nameOverride }}
spec:
  replicas: 1
  selector:
    matchLabels:
      app: {{ .Values.nameOverride }}
  template:
    metadata:
      labels:
        app: {{ .Values.nameOverride }}
    spec:
      containers:
        - name: postgres
          image: "{{ .Values.image.repository }}:{{ .Values.image.tag }}"
          ports:
            - containerPort: 5432
          envFrom:
            - secretRef:
                name: {{ .Values.nameOverride }}-credentials
          volumeMounts:
            - name: data
              mountPath: /var/lib/pgsql/data
      volumes:
        - name: data
          persistentVolumeClaim:
            claimName: {{ .Values.nameOverride }}-data
```

**Step 6: `helm/postgres/templates/service.yaml`**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: {{ .Values.nameOverride }}
  namespace: {{ .Values.namespace }}
  labels:
    app: {{ .Values.nameOverride }}
spec:
  ports:
    - name: postgres
      port: 5432
      targetPort: 5432
  selector:
    app: {{ .Values.nameOverride }}
```

**Step 7: Lint the chart**

Run: `helm lint helm/postgres`
Expected: `0 chart(s) failed`

**Step 8: Commit**

```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
git add helm/postgres/
git commit -m "Add PostgreSQL Helm chart for BlogApi"
```

---

## Task 9: Add BlogApi Helm chart and OpenShift build resources

**Files:**
- Create: `openshift/blogapi-imagestream.yaml`
- Create: `openshift/blogapi-BC-docker.yaml`
- Create: `helm/blogapi/Chart.yaml`
- Create: `helm/blogapi/values.yaml`
- Create: `helm/blogapi/templates/deploymentconfig.yaml`
- Create: `helm/blogapi/templates/service.yaml`
- Create: `helm/blogapi/templates/route.yaml`
- Create: `helm/blogapi/templates/secret.yaml`

**Step 1: `openshift/blogapi-imagestream.yaml`**

```yaml
kind: ImageStream
apiVersion: image.openshift.io/v1
metadata:
  name: blogapi
  namespace: devops
```

**Step 2: `openshift/blogapi-BC-docker.yaml`**

```yaml
kind: BuildConfig
apiVersion: build.openshift.io/v1
metadata:
  name: blogapi-build
  namespace: devops
spec:
  source:
    type: Git
    git:
      uri: https://github.com/<org>/<repo>.git
    contextDir: backend
  strategy:
    type: Docker
    dockerStrategy:
      dockerfilePath: Dockerfile
  output:
    to:
      kind: ImageStreamTag
      name: 'blogapi:latest'
```

Note: Replace `<org>/<repo>` with this repository's actual Git URL (check `git remote -v` or the existing Jenkinsfile source URL for the correct value already used in this repo).

**Step 3: `helm/blogapi/Chart.yaml`**

```yaml
apiVersion: v2
name: blogapi
description: A Helm chart for the BlogApi backend
type: application
version: 0.1.0
appVersion: "1.0.0"
```

**Step 4: `helm/blogapi/values.yaml`**

```yaml
replicaCount: 1
nameOverride: blogapi
namespace: devops
strategy: Rolling
image:
  repository: blogapi
  tag: latest
  namespace: devops
apiKey: ""
db:
  host: blogapi-postgres
  port: 5432
  name: blogdb
  username: blogapi
  password: ""
```

**Step 5: `helm/blogapi/templates/secret.yaml`**

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: {{ .Values.nameOverride }}-secrets
  namespace: {{ .Values.namespace }}
type: Opaque
stringData:
  ApiKey: {{ .Values.apiKey | quote }}
  ConnectionStrings__BlogDb: "Host={{ .Values.db.host }};Port={{ .Values.db.port }};Database={{ .Values.db.name }};Username={{ .Values.db.username }};Password={{ .Values.db.password }}"
```

**Step 6: `helm/blogapi/templates/deploymentconfig.yaml`**

```yaml
kind: DeploymentConfig
apiVersion: apps.openshift.io/v1
metadata:
  name: {{ .Values.nameOverride }}
  namespace: {{ .Values.namespace }}
  labels:
    app: {{ .Values.nameOverride }}
spec:
  strategy:
    type: {{ .Values.strategy }}
  triggers:
    - type: ImageChange
      imageChangeParams:
        automatic: true
        containerNames:
          - {{ .Values.nameOverride }}
        from:
          kind: ImageStreamTag
          namespace: {{ .Values.image.namespace }}
          name: {{ .Values.image.repository }}:{{ .Values.image.tag }}
    - type: ConfigChange
  replicas: {{ .Values.replicaCount }}
  selector:
    app: {{ .Values.nameOverride }}
    deploymentconfig: {{ .Values.nameOverride }}
  template:
    metadata:
      labels:
        app: {{ .Values.nameOverride }}
        deploymentconfig: {{ .Values.nameOverride }}
    spec:
      containers:
        - name: {{ .Values.nameOverride }}
          image: >-
            image-registry.openshift-image-registry.svc:5000/{{ .Values.image.namespace }}/{{ .Values.image.repository }}:{{ .Values.image.tag }}
          ports:
            - containerPort: 8080
              protocol: TCP
          envFrom:
            - secretRef:
                name: {{ .Values.nameOverride }}-secrets
```

**Step 7: `helm/blogapi/templates/service.yaml`**

```yaml
kind: Service
apiVersion: v1
metadata:
  name: {{ .Values.nameOverride }}
  namespace: {{ .Values.namespace }}
  labels:
    app: {{ .Values.nameOverride }}
spec:
  ports:
    - name: 8080-tcp
      protocol: TCP
      port: 8080
      targetPort: 8080
  selector:
    app: {{ .Values.nameOverride }}
    deploymentconfig: {{ .Values.nameOverride }}
```

**Step 8: `helm/blogapi/templates/route.yaml`**

```yaml
kind: Route
apiVersion: route.openshift.io/v1
metadata:
  name: {{ .Values.nameOverride }}
  namespace: {{ .Values.namespace }}
  labels:
    app: {{ .Values.nameOverride }}
spec:
  to:
    kind: Service
    name: {{ .Values.nameOverride }}
    weight: 100
  port:
    targetPort: 8080-tcp
  tls:
    termination: edge
    insecureEdgeTerminationPolicy: Allow
  wildcardPolicy: None
```

**Step 9: Lint the chart**

Run: `helm lint helm/blogapi`
Expected: `0 chart(s) failed`

**Step 10: Commit**

```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
git add openshift/blogapi-imagestream.yaml openshift/blogapi-BC-docker.yaml helm/blogapi/
git commit -m "Add BlogApi Helm chart and OpenShift build resources"
```

---

## Task 10: Document deployment steps

**Files:**
- Modify: `readme.md`

**Step 1: Append a new section to `readme.md`**

```markdown

## Deploying the BlogApi backend + PostgreSQL

1. Apply the ImageStream and BuildConfig, then start a build:

   ```bash
   oc project devops
   oc apply -f openshift/blogapi-imagestream.yaml
   oc apply -f openshift/blogapi-BC-docker.yaml
   oc start-build blogapi-build --follow
   ```

2. Install PostgreSQL (set a real password; do not commit it):

   ```bash
   helm install blogapi-postgres ./helm/postgres/ \
     --set credentials.password=<choose-a-strong-password>
   ```

3. Install BlogApi (must match the PostgreSQL password from step 2, and choose a strong API key):

   ```bash
   helm install blogapi ./helm/blogapi/ \
     --set apiKey=<choose-a-strong-api-key> \
     --set db.password=<same-password-as-step-2>
   ```

4. Verify:

   ```bash
   oc get route blogapi
   curl https://<route-host>/health
   curl -H "X-API-Key: <api-key>" https://<route-host>/api/posts
   ```
```

**Step 2: Commit**

```bash
cd /Users/alexandru.chiscari/git/poc-shift-left/openshift-demo-pipeline
git add readme.md
git commit -m "Document BlogApi and PostgreSQL deployment steps"
```

---

## Final Verification

Run the full backend test suite one more time to confirm everything is green before considering this plan complete:

Run: `dotnet test backend/BlogApi.sln`
Expected: All tests pass, no warnings/errors.

Run: `helm lint helm/blogapi helm/postgres`
Expected: `0 chart(s) failed` for both.
