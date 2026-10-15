using BisiTracker.Api.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace BisiTracker.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public void ProductionRefusesDevelopmentAuthentication()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:DevelopmentEnabled"] = "true" });
        Assert.Throws<InvalidOperationException>(() => builder.AddOrganizerAuthentication());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("http://identity.example", "bisi-api")]
    [InlineData("https://identity.example", "")]
    public void ProductionFailsClosedWithoutHttpsAuthorityAndAudience(string authority, string audience)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:DevelopmentEnabled"] = "false", ["Authentication:Authority"] = authority,
            ["Authentication:Audience"] = audience
        });
        Assert.Throws<InvalidOperationException>(() => builder.AddOrganizerAuthentication());
    }
}
