using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace BisiTracker.Api.Auth;

public sealed class DevelopmentIdentity
{
    public SymmetricSecurityKey Key { get; } = new(RandomNumberGenerator.GetBytes(64));
    public const string Issuer = "BisiTracker.Development";
    public const string Audience = "BisiTracker.Api";

    public DevelopmentSession SignIn(string profile)
    {
        var name = profile == "primary" ? "Asha" : "Meera";
        var expires = DateTime.UtcNow.AddHours(8);
        var token = new JwtSecurityToken(Issuer, Audience,
            [new Claim("sub", $"dev-{profile}"), new Claim("name", name)], expires: expires,
            signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));
        return new DevelopmentSession(new JwtSecurityTokenHandler().WriteToken(token), expires, name);
    }
}

public sealed record DevelopmentSession(string AccessToken, DateTime ExpiresAt, string Name);
public sealed record OrganizerView(string Name);

public static class AuthenticationSetup
{
    public static bool AddOrganizerAuthentication(this WebApplicationBuilder builder)
    {
        var requested = builder.Configuration.GetValue<bool>("Authentication:DevelopmentEnabled");
        if (requested && !builder.Environment.IsDevelopment())
            throw new InvalidOperationException("Development authentication must never be enabled outside Development.");
        var auth = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
        if (requested)
        {
            var identity = new DevelopmentIdentity();
            builder.Services.AddSingleton(identity);
            auth.AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = DevelopmentIdentity.Issuer,
                    ValidateAudience = true, ValidAudience = DevelopmentIdentity.Audience,
                    ValidateIssuerSigningKey = true, IssuerSigningKey = identity.Key,
                    ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "name"
                };
            });
        }
        else
        {
            var authority = builder.Configuration["Authentication:Authority"];
            var audience = builder.Configuration["Authentication:Audience"];
            if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) || issuer.Scheme != "https" || string.IsNullOrWhiteSpace(audience))
                throw new InvalidOperationException("Configure an HTTPS OIDC authority and API audience before starting.");
            auth.AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "name";
            });
        }
        builder.Services.AddAuthorization(options => options.AddPolicy("organizer", policy =>
            policy.RequireAuthenticatedUser().RequireClaim("sub").RequireClaim("iss")));
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            options.Events.OnTokenValidated = context =>
            {
                var issuer = context.Principal?.FindFirstValue("iss");
                var subject = context.Principal?.FindFirstValue("sub");
                if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject) ||
                    $"{issuer.Length}:{issuer}{subject}".Length > 512)
                    context.Fail("The token must contain a supported issuer and subject.");
                return Task.CompletedTask;
            });
        return requested;
    }

    public static string Owner(this ClaimsPrincipal user)
    {
        var issuer = user.FindFirstValue("iss")!;
        var subject = user.FindFirstValue("sub")!;
        // Length-prefixed issuer avoids collisions between provider/subject combinations.
        return $"{issuer.Length}:{issuer}{subject}";
    }
}
