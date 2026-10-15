using System.Net.Http.Headers;
using System.Net.Http.Json;
using BisiTracker.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace BisiTracker.Tests;

public sealed class ApiFixture : IAsyncLifetime
{
    private readonly string database = $"bisi_test_{Guid.NewGuid():N}";
    private readonly string adminConnection = Environment.GetEnvironmentVariable("BISI_TEST_DATABASE")
        ?? "Host=localhost;Port=54329;Database=postgres;Username=bisi;Password=bisi-local-only";
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
        await create.ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(adminConnection) { Database = database }.ConnectionString;
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<LedgerDbContext>>();
                services.AddDbContext<LedgerDbContext>(options => options.UseNpgsql(connection));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTime());
            });
        });
        using var client = Factory.CreateClient();
        var ready = await client.GetAsync("/health/ready");
        ready.EnsureSuccessStatusCode();
    }

    public async Task<HttpClient> Organizer(string profile = "primary")
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/dev/sign-in", new { profile });
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<Session>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        // Delete only this fixture's freshly created, randomly named test database.
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    private sealed record Session(string AccessToken);
    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    }
}
