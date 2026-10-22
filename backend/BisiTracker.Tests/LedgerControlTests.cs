using System.Net;
using System.Net.Http.Json;
using BisiTracker.Api.Data;
using BisiTracker.Api.Domain;
using BisiTracker.Api.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BisiTracker.Tests;

public sealed class LedgerControlTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly DateOnly PaidOn = new(2026, 10, 12);
    private static Task<LedgerView?> Read(HttpClient client, Guid id) => client.GetFromJsonAsync<LedgerView>($"/api/groups/{id}");
    private static async Task<LedgerView> Group(HttpClient client, bool active = false)
    {
        var response = await client.PostAsJsonAsync("/api/groups", new CreateGroupRequest(Guid.NewGuid(), "Controls test", 100,
            new DateOnly(2026, 10, 1), 10, "en", ["Asha", "Meera", "Sunita"]));
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<MutationResult>())!.GroupId;
        if (active) (await client.PostAsJsonAsync($"/api/groups/{id}/activate", new ActivateRequest(Guid.NewGuid(), 1))).EnsureSuccessStatusCode();
        return (await Read(client, id))!;
    }

    private static string CyclePath(LedgerView group) => $"/api/groups/{group.Id}/cycles/{group.Cycles[0].Id}";
    private static async Task<LedgerView> Settle(HttpClient client, LedgerView group)
    {
        foreach (var member in group.Members)
            (await client.PostAsJsonAsync($"{CyclePath(group)}/members/{member.Id}/payments",
                new RecordPaymentRequest(Guid.NewGuid(), 100, PaidOn, "Cash", null))).EnsureSuccessStatusCode();
        group = (await Read(client, group.Id))!;
        (await client.PostAsJsonAsync($"{CyclePath(group)}/payout",
            new RecordPayoutRequest(Guid.NewGuid(), group.Version, 300, PaidOn, null))).EnsureSuccessStatusCode();
        return (await Read(client, group.Id))!;
    }

    [Fact]
    public async Task DraftEditRenamesRemovesAndSwapsPositionsWithoutChangingRetainedIds()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client);
        var request = new EditMembersRequest(Guid.NewGuid(), group.Version,
            [new(group.Members[2].Id, "Sunita Patil"), new(group.Members[0].Id, "Asha")]);
        var path = $"/api/groups/{group.Id}/members/edit";
        (await client.PostAsJsonAsync(path, request)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(path, request)).EnsureSuccessStatusCode();
        var edited = (await Read(client, group.Id))!;
        Assert.Equal(group.Version + 1, edited.Version);
        Assert.Equal([group.Members[2].Id, group.Members[0].Id], edited.Members.Select(x => x.Id));
        Assert.Equal([1, 2], edited.Members.Select(x => x.Position));
        Assert.Equal("Sunita Patil", edited.Members[0].Name);
        Assert.Single(edited.History, x => x.Kind == "DraftMembersEdited");
        (await client.PostAsJsonAsync($"/api/groups/{group.Id}/activate", new ActivateRequest(Guid.NewGuid(), edited.Version))).EnsureSuccessStatusCode();
        var active = (await Read(client, group.Id))!;
        Assert.Equal(edited.Members.Select(x => x.Id), active.Cycles.Select(x => x.RecipientId));
        Assert.All(active.Cycles, x => Assert.Equal(200, x.ExpectedPaise));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = active.Version })).StatusCode);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    [InlineData("blank")]
    [InlineData("null")]
    public async Task InvalidDraftEditHasNoPartialEffects(string error)
    {
        using var client = await fixture.Organizer();
        var group = await Group(client);
        DraftMemberEdit[]? members = error switch
        {
            "duplicate" => [new(group.Members[0].Id, "A"), new(group.Members[0].Id, "B")],
            "foreign" => [new(Guid.NewGuid(), "Unknown")],
            "blank" => [new(group.Members[0].Id, "  ")],
            _ => null
        };
        var response = await client.PostAsJsonAsync($"/api/groups/{group.Id}/members/edit",
            new EditMembersRequest(Guid.NewGuid(), group.Version, members!));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var unchanged = (await Read(client, group.Id))!;
        Assert.Equal(group.Version, unchanged.Version);
        Assert.Equal(group.Members, unchanged.Members);
        Assert.Single(unchanged.History);
    }

    [Fact]
    public async Task ConcurrentDraftEditAndActivationCannotMixRules()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client);
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/groups/{group.Id}/members/edit", new EditMembersRequest(Guid.NewGuid(), group.Version,
                group.Members.Reverse().Select(x => new DraftMemberEdit(x.Id, x.Name)).ToArray())),
            client.PostAsJsonAsync($"/api/groups/{group.Id}/activate", new ActivateRequest(Guid.NewGuid(), group.Version)));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var result = (await Read(client, group.Id))!;
        Assert.Equal(2, result.Version);
        Assert.Equal([1, 2, 3], result.Members.Select(x => x.Position));
        if (result.Status == "Active") Assert.Equal(group.Members, result.Members);
        else Assert.Empty(result.Cycles);
    }

    [Fact]
    public async Task PayoutCorrectionRetainsOriginalAndAllowsReplacementExactlyOnce()
    {
        using var client = await fixture.Organizer();
        var group = await Settle(client, await Group(client, true));
        var original = group.Cycles[0].Payout!.Entries[0];
        var path = $"/api/groups/{group.Id}/payouts/{original.Id}/reverse";
        var request = new ReversePayoutRequest(Guid.NewGuid(), group.Version, "Wrong payment date");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, request with { Reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, request with { ExpectedVersion = 1 })).StatusCode);
        var first = await client.PostAsJsonAsync(path, request);
        first.EnsureSuccessStatusCode();
        var retry = await client.PostAsJsonAsync(path, request);
        retry.EnsureSuccessStatusCode();
        Assert.Equal(await first.Content.ReadFromJsonAsync<MutationResult>(), await retry.Content.ReadFromJsonAsync<MutationResult>());
        group = (await Read(client, group.Id))!;
        var payout = group.Cycles[0].Payout!;
        Assert.Equal(0, payout.AmountPaise);
        Assert.Equal(300, payout.OutstandingPaise);
        Assert.Equal(original.Id, Assert.Single(payout.Entries).Id);
        Assert.Equal(request.Reason, payout.Entries[0].ReversalReason);
        Assert.Equal(300, group.Cycles[0].CollectedPaise);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path,
            request with { OperationId = Guid.NewGuid(), ExpectedVersion = group.Version })).StatusCode);
        (await client.PostAsJsonAsync($"{CyclePath(group)}/payout",
            new RecordPayoutRequest(Guid.NewGuid(), group.Version, 300, PaidOn, null))).EnsureSuccessStatusCode();
        group = (await Read(client, group.Id))!;
        Assert.Equal(300, group.Cycles[0].Payout!.AmountPaise);
        Assert.Equal(2, group.Cycles[0].Payout!.Entries.Length);
        Assert.Single(group.History, x => x.Kind == "PayoutReversed");
    }

    [Fact]
    public async Task CloseRequiresSettlementAndReopeningIsAuditedBeforeCorrections()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client, true);
        var cyclePath = CyclePath(group);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{cyclePath}/close",
            new CloseCycleRequest(Guid.NewGuid(), group.Version))).StatusCode);
        group = await Settle(client, group);
        var close = new CloseCycleRequest(Guid.NewGuid(), group.Version);
        (await client.PostAsJsonAsync($"{cyclePath}/close", close)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"{cyclePath}/close", close)).EnsureSuccessStatusCode();
        group = (await Read(client, group.Id))!;
        Assert.NotNull(group.Cycles[0].ClosedAt);
        Assert.Null(group.Cycles[1].ClosedAt);
        var paymentId = group.Cycles[0].Dues[0].Payments[0].Id;
        var payoutId = group.Cycles[0].Payout!.Entries[0].Id;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{cyclePath}/members/{group.Members[0].Id}/payments",
            new RecordPaymentRequest(Guid.NewGuid(), 1, PaidOn, "Cash", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/groups/{group.Id}/payments/{paymentId}/reverse",
            new ReversePaymentRequest(Guid.NewGuid(), group.Version, "Wrong entry"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{cyclePath}/payout",
            new RecordPayoutRequest(Guid.NewGuid(), group.Version, 1, PaidOn, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/groups/{group.Id}/payouts/{payoutId}/reverse",
            new ReversePayoutRequest(Guid.NewGuid(), group.Version, "Wrong entry"))).StatusCode);
        var reopen = new ReopenCycleRequest(Guid.NewGuid(), group.Version, "Check the recorded payout date");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{cyclePath}/reopen", reopen with { Reason = " " })).StatusCode);
        (await client.PostAsJsonAsync($"{cyclePath}/reopen", reopen)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"{cyclePath}/reopen", reopen)).EnsureSuccessStatusCode();
        group = (await Read(client, group.Id))!;
        Assert.Null(group.Cycles[0].ClosedAt);
        Assert.Single(group.History, x => x.Kind == "CycleClosed");
        Assert.Contains(group.History, x => x.Kind == "CycleReopened" && x.Description.Contains(reopen.Reason));
        (await client.PostAsJsonAsync($"/api/groups/{group.Id}/payouts/{payoutId}/reverse",
            new ReversePayoutRequest(Guid.NewGuid(), group.Version, "Correct date"))).EnsureSuccessStatusCode();
        group = (await Read(client, group.Id))!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{cyclePath}/close",
            new CloseCycleRequest(Guid.NewGuid(), group.Version))).StatusCode);
    }

    [Fact]
    public async Task HistoryBeyondTwoHundredIsCompleteAndDetectsChangesBetweenPages()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client, true);
        // Seed a long history with equal timestamps to exercise the stable secondary sort.
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
            var stored = await db.Groups.SingleAsync(x => x.Id == group.Id);
            db.AuditEvents.AddRange(Enumerable.Range(0, 215).Select(i => new AuditEvent
            {
                GroupId = group.Id, ActorId = stored.OwnerId, Kind = "TestHistory", Description = $"Historical entry {i}",
                RecordedAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)
            }));
            stored.Version += 215;
            await db.SaveChangesAsync();
        }
        group = (await Read(client, group.Id))!;
        Assert.Equal(50, group.History.Length);
        Assert.Equal(2, group.HistoryNextPage);
        var items = group.History.ToList();
        int? next = group.HistoryNextPage;
        while (next.HasValue)
        {
            var page = (await client.GetFromJsonAsync<AuditPage>($"/api/groups/{group.Id}/history?page={next}&expectedVersion={group.Version}"))!;
            items.AddRange(page.Items);
            next = page.NextPage;
        }
        Assert.Equal(217, items.Count);
        Assert.Equal(items.Count, items.Select(x => x.Id).Distinct().Count());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/groups/{group.Id}/history?page=2")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/groups/{group.Id}/history?pageSize=101")).StatusCode);
        (await client.PostAsJsonAsync($"{CyclePath(group)}/members/{group.Members[0].Id}/payments",
            new RecordPaymentRequest(Guid.NewGuid(), 1, PaidOn, "Cash", null))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync($"/api/groups/{group.Id}/history?page=2&expectedVersion={group.Version}")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentClosingAndPayoutCorrectionCannotLeaveAnUnsettledClosedCycle()
    {
        using var client = await fixture.Organizer();
        var group = await Settle(client, await Group(client, true));
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"{CyclePath(group)}/close", new CloseCycleRequest(Guid.NewGuid(), group.Version)),
            client.PostAsJsonAsync($"/api/groups/{group.Id}/payouts/{group.Cycles[0].Payout!.Entries[0].Id}/reverse",
                new ReversePayoutRequest(Guid.NewGuid(), group.Version, "Correct amount")));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var result = (await Read(client, group.Id))!;
        var cycle = result.Cycles[0];
        Assert.Equal(group.Version + 1, result.Version);
        if (cycle.ClosedAt.HasValue)
        {
            Assert.Equal(300, cycle.Payout!.AmountPaise);
            Assert.DoesNotContain(result.History, x => x.Kind == "PayoutReversed");
        }
        else
        {
            Assert.Equal(0, cycle.Payout!.AmountPaise);
            Assert.DoesNotContain(result.History, x => x.Kind == "CycleClosed");
        }
    }

    [Fact]
    public async Task NewControlsAreScopedToTheOrganizer()
    {
        using var owner = await fixture.Organizer();
        using var other = await fixture.Organizer("secondary");
        var group = await Settle(owner, await Group(owner, true));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/groups/{group.Id}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/groups/{group.Id}/members/edit",
            new EditMembersRequest(Guid.NewGuid(), group.Version, []))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"{CyclePath(group)}/close",
            new CloseCycleRequest(Guid.NewGuid(), group.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"{CyclePath(group)}/reopen",
            new ReopenCycleRequest(Guid.NewGuid(), group.Version, "Reason"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/groups/{group.Id}/payouts/{group.Cycles[0].Payout!.Entries[0].Id}/reverse",
            new ReversePayoutRequest(Guid.NewGuid(), group.Version, "Reason"))).StatusCode);
    }
}
