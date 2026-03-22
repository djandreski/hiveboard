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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(static serviceProvider =>
{
    var httpContext = serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var user = httpContext?.User;
    var agentContext = new AgentContext
    {
        IsAdmin = string.Equals(
            user?.FindFirst("IsAdmin")?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase),
        AgentName = user?.FindFirst("AgentName")?.Value ?? string.Empty
    };

    if (Guid.TryParse(user?.FindFirst("AgentId")?.Value, out var agentId))
        agentContext.AgentId = agentId;

    if (Enum.TryParse<AgentType>(user?.FindFirst("AgentType")?.Value, out var agentType))
        agentContext.AgentType = agentType;

    if (Guid.TryParse(user?.FindFirst("OrganizationId")?.Value, out var organizationId))
        agentContext.OrganizationId = organizationId;

    return agentContext;
});
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
        Description = "API key authentication. Use an agent key (Any/Orchestrator) or admin key for admin endpoints."
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
    .AddPolicy("OrchestratorOnly", policy =>
        policy.RequireAssertion(context =>
            context.User.FindFirst("AgentType")?.Value == "Orchestrator" ||
            context.User.FindFirst("IsAdmin")?.Value == "true"));

builder.Services.AddScoped<AdminKeyProvider>();

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
        HiveboardDbSeeder.SeedDevelopmentData(db);
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

app.Run();
