using Hiveboard.Api.Application;
using Hiveboard.Api.Auth;
using Hiveboard.Api.Endpoints;
using Hiveboard.Core.Enums;
using Hiveboard.Infrastructure;
using Hiveboard.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
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

app.MapAgentEndpoints();
app.MapAdminKeyEndpoints();
app.MapProjectEndpoints();
app.MapEpicEndpoints();
app.MapTaskEndpoints();
app.MapTaskStatusEndpoints();
app.MapDependencyEndpoints();

app.Run();

public partial class Program
{
}
