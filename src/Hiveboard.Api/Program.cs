using Hiveboard.Infrastructure;
using Hiveboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHiveboardInfrastructure(builder.Configuration);

var app = builder.Build();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<HiveboardDbContext>();
db.Database.Migrate();

if (app.Environment.IsDevelopment())
{
    HiveboardDbSeeder.SeedDevelopmentData(db);
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
