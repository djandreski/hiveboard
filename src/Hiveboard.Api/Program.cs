using Hiveboard.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHiveboardInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
