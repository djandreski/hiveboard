using Hiveboard.Api.Auth;
using Hiveboard.Api.Endpoints;
using Hiveboard.Infrastructure;
using Hiveboard.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHiveboardInfrastructure(builder.Configuration);

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
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapAgentEndpoints();
app.MapAdminKeyEndpoints();

app.Run();
