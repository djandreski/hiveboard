using Hiveboard.Api.Application;
using Hiveboard.Api.Auth;
using Hiveboard.Api.Endpoints;
using Hiveboard.Api.Mcp;
using Hiveboard.Core.Enums;
using Hiveboard.Infrastructure;
using Hiveboard.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

if (OperatingSystem.IsWindows())
{
    builder.Logging.AddFilter<EventLogLoggerProvider>(level => false);
}

builder.Services.AddHiveboardInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHiveboardApplication();
builder.Services.AddHiveboardMcpServer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hiveboard REST API",
        Version = "v1",
        Description = "Headless project management API for multi-agent software workflows."
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Description = "API key authentication. Use the coordinator/admin key for control-plane actions, an orchestrator agent key for optional orchestration, or a worker key for worker flows."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("ApiKey", document, null),
            new List<string>()
        }
    });
});

// Authentication
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);

// Authorization policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy =>
        policy.RequireAssertion(context =>
            context.User.FindFirst("IsAdmin")?.Value == "true"))
    .AddPolicy("CoordinatorOrOrchestratorOnly", policy =>
        policy.RequireAssertion(context =>
            context.User.FindFirst("AgentType")?.Value == nameof(AgentType.Orchestrator) ||
            context.User.FindFirst("IsAdmin")?.Value == "true"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Hiveboard REST API v1");
        options.RoutePrefix = "swagger";
    });
}

// Database initialization
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HiveboardDbContext>();
    db.Database.Migrate();

    if (app.Environment.IsDevelopment())
    {
        var seederType = typeof(HiveboardDbContext).Assembly.GetType("Hiveboard.Infrastructure.Data.HiveboardDbSeeder")
            ?? throw new InvalidOperationException("Infrastructure development seeder type was not found.");
        var seedMethod = seederType.GetMethod("SeedDevelopmentData", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("Infrastructure development seeder entry point was not found.");
        seedMethod.Invoke(null, [db]);
    }

    // Ensure admin key exists
    var adminKeyProvider = scope.ServiceProvider.GetRequiredService<AdminKeyProvider>();
    await adminKeyProvider.EnsureAdminKeyAsync();
}

// Serve the bundled React SPA at /dashboard when its build output is
// present in wwwroot/dashboard. The dashboard is intentionally NOT built
// on every `dotnet build` — operators run `npm run build:bundle` (or
// build with `-p:BuildDashboardAssets=true`) for self-hosted packaging.
var dashboardRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "dashboard");
if (Directory.Exists(dashboardRoot) && File.Exists(Path.Combine(dashboardRoot, "index.html")))
{
    var dashboardFiles = new PhysicalFileProvider(dashboardRoot);
    var dashboardOptions = new StaticFileOptions
    {
        FileProvider = dashboardFiles,
        RequestPath = "/dashboard",
    };

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = dashboardFiles,
        RequestPath = "/dashboard",
        DefaultFileNames = new List<string> { "index.html" },
    });
    app.UseStaticFiles(dashboardOptions);

    // Client-side router fallback: any /dashboard/* request that didn't
    // match a static asset returns the SPA shell so deep links work.
    async Task ServeIndex(HttpContext context)
    {
        var index = dashboardFiles.GetFileInfo("index.html");
        if (!index.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(index);
    }

    app.MapGet("/dashboard", ServeIndex).AllowAnonymous();
    app.MapFallback("/dashboard/{*path:nonfile}", ServeIndex).AllowAnonymous();
}

app.UseHiveboardMcpApiKeyFallback(HiveboardMcpServer.DefaultEndpointPath);
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithTags("System")
    .WithName("GetHealth")
    .WithSummary("Health check")
    .WithDescription("Auth: none. Returns the current API host health status.")
    .Produces(StatusCodes.Status200OK);

// MCP server (Model Context Protocol) — exposes Hiveboard tools and
// resources at /mcp via HTTP/SSE transport. The same X-Api-Key auth flow
// used by the REST API gates the endpoint; see Mcp/HiveboardMcpServer.cs.
app.MapMcp(HiveboardMcpServer.DefaultEndpointPath)
    .RequireAuthorization();

app.MapAgentEndpoints();
app.MapAdminKeyEndpoints();
app.MapProjectEndpoints();
app.MapEpicEndpoints();
app.MapTaskEndpoints();
app.MapTaskNoteEndpoints();
app.MapTaskStatusEndpoints();
app.MapTaskDecompositionEndpoints();
app.MapDependencyEndpoints();
app.MapDecisionEndpoints();
app.MapNotificationEndpoints();

app.Run();

public partial class Program
{
}
