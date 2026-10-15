using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using BisiTracker.Api.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BisiTracker.Tests;

public sealed class TokenApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Theory]
    [InlineData("expired")]
    [InlineData("wrong_audience")]
    [InlineData("missing_subject")]
    [InlineData("oversized_subject")]
    public async Task InvalidIdentityCannotAccessFinancialRecords(string kind)
    {
        var key = fixture.Factory.Services.GetRequiredService<DevelopmentIdentity>().Key;
        var claims = kind == "missing_subject" ? new List<Claim>() : [new Claim("sub", kind == "oversized_subject" ? new string('a', 600) : "dev-primary")];
        var token = new JwtSecurityToken(DevelopmentIdentity.Issuer, kind == "wrong_audience" ? "another-api" : DevelopmentIdentity.Audience,
            claims, expires: kind == "expired" ? DateTime.UtcNow.AddMinutes(-2) : DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/groups")).StatusCode);
    }
}
