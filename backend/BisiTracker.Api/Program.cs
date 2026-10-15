using System.Threading.RateLimiting;
using BisiTracker.Api.Auth;
using BisiTracker.Api.Data;
using BisiTracker.Api.Features;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var developmentAuth = builder.AddOrganizerAuthentication();
builder.Services.AddDbContext<LedgerDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Configure ConnectionStrings:Database.")));
builder.Services.AddScoped<LedgerService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.IsAuthenticated == true ? context.User.Owner() : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception exception) when (exception is LedgerException or DbUpdateConcurrencyException or DbUpdateException)
    {
        var status = 409;
        var code = "concurrent_change";
        var detail = "Another change was saved at the same time. Refresh and retry with the same operation ID.";
        if (exception is LedgerException error)
        {
            status = error.StatusCode; code = error.Code; detail = error.Message;
        }
        else if (exception is DbUpdateException && exception is not DbUpdateConcurrencyException &&
                 exception.InnerException is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw;
        }
        context.Response.Clear();
        await Results.Problem(statusCode: status, title: code, detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code }).ExecuteAsync(context);
    }
});
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (LedgerDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));

if (developmentAuth)
{
    app.MapPost("/api/dev/sign-in", (DevelopmentSignIn request, DevelopmentIdentity identity) =>
    {
        LedgerRules.Require(request.Profile is "primary" or "secondary", "profile", "Choose primary or secondary development profile.");
        return Results.Ok(identity.SignIn(request.Profile));
    }).WithName("DevelopmentSignIn").Produces<DevelopmentSession>().RequireRateLimiting("api");
}

var api = app.MapGroup("/api").RequireAuthorization("organizer").RequireRateLimiting("api");
api.MapGet("/me", (HttpContext context) => new OrganizerView(context.User.FindFirst("name")?.Value ?? "Organizer")).WithName("GetOrganizer");
api.MapGet("/groups", (HttpContext context, LedgerService ledger, CancellationToken ct, int page = 1, int pageSize = 20) =>
    ledger.List(context.User.Owner(), page, pageSize, ct)).WithName("ListGroups");
api.MapGet("/groups/{id:guid}", (Guid id, HttpContext context, LedgerService ledger, CancellationToken ct) =>
    ledger.Get(context.User.Owner(), id, ct)).WithName("GetLedger");
api.MapPost("/groups", (CreateGroupRequest request, HttpContext context, LedgerService ledger, CancellationToken ct) =>
    ledger.Create(context.User.Owner(), request, ct)).WithName("CreateGroup");
api.MapPost("/groups/{id:guid}/members", (Guid id, AddMemberRequest request, HttpContext context, LedgerService ledger, CancellationToken ct) =>
    ledger.AddMember(context.User.Owner(), id, request, ct)).WithName("AddMember");
api.MapPost("/groups/{id:guid}/activate", (Guid id, ActivateRequest request, HttpContext context, LedgerService ledger, CancellationToken ct) =>
    ledger.Activate(context.User.Owner(), id, request, ct)).WithName("ActivateGroup");
api.MapPost("/groups/{id:guid}/cycles/{cycleId:guid}/members/{memberId:guid}/payments",
    (Guid id, Guid cycleId, Guid memberId, RecordPaymentRequest request, HttpContext context, LedgerService ledger, CancellationToken ct) =>
        ledger.Payment(context.User.Owner(), id, cycleId, memberId, request, ct)).WithName("RecordPayment");
api.MapPost("/groups/{id:guid}/payments/{paymentId:guid}/reverse",
    (Guid id, Guid paymentId, ReversePaymentRequest request, HttpContext context, LedgerService ledger, CancellationToken ct) =>
        ledger.Reverse(context.User.Owner(), id, paymentId, request, ct)).WithName("ReversePayment");
api.MapPost("/groups/{id:guid}/cycles/{cycleId:guid}/payout", (Guid id, Guid cycleId, RecordPayoutRequest request,
    HttpContext context, LedgerService ledger, CancellationToken ct) => ledger.Payout(context.User.Owner(), id, cycleId, request, ct)).WithName("RecordPayout");

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Apply production migrations as a deployment step, not on startup.");
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.MigrateAsync();
}
app.Run();

public sealed record DevelopmentSignIn(string Profile);
public partial class Program;
