using Hiveboard.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hiveboard.Tests;

public class TestHostLoggingConfigurationTests
{
    [Fact]
    public void TestingHost_Suppresses_EfCore_Command_And_Migration_Info_Logs()
    {
        using var app = new HiveboardApiFactory();
        using var client = app.CreateAnonymousClient();
        using var scope = app.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Assert.Equal("Warning", configuration["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"]);
        Assert.Equal("Warning", configuration["Logging:LogLevel:Microsoft.EntityFrameworkCore.Migrations"]);
    }
}
