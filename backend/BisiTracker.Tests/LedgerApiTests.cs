using System.Net;
using System.Net.Http.Json;
using BisiTracker.Api.Features;

namespace BisiTracker.Tests;

public sealed class LedgerApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly DateOnly PaidOn = new(2026, 10, 12);

    private static async Task<LedgerView> Group(HttpClient client, bool active = true)
    {
        var request = new CreateGroupRequest(Guid.NewGuid(), "Neighbourhood Bhishi", 100_000,
            new DateOnly(2026, 10, 1), 10, "mr", ["Asha", "Meera", "Sunita"]);
        var response = await client.PostAsJsonAsync("/api/groups", request);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<MutationResult>())!;
        if (active)
        {
            response = await client.PostAsJsonAsync($"/api/groups/{result.GroupId}/activate", new ActivateRequest(Guid.NewGuid(), 1));
            response.EnsureSuccessStatusCode();
        }
        return (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{result.GroupId}"))!;
    }

    private static string PaymentPath(LedgerView group, int member = 0) =>
        $"/api/groups/{group.Id}/cycles/{group.Cycles[0].Id}/members/{group.Members[member].Id}/payments";

    [Fact]
    public async Task DraftActivationBuildsFixedScheduleAndFreezesMembers()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client, false);
        Assert.Empty(group.Cycles);
        var member = await client.PostAsJsonAsync($"/api/groups/{group.Id}/members",
            new AddMemberRequest(Guid.NewGuid(), group.Version, "Rekha"));
        member.EnsureSuccessStatusCode();
        var activate = await client.PostAsJsonAsync($"/api/groups/{group.Id}/activate", new ActivateRequest(Guid.NewGuid(), 2));
        activate.EnsureSuccessStatusCode();
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        Assert.Equal(4, group.Cycles.Length);
        Assert.Equal(new DateOnly(2027, 1, 10), group.Cycles[3].DueDate);
        Assert.Equal(4, group.Cycles.Select(x => x.RecipientId).Distinct().Count());
        Assert.All(group.Cycles, x => Assert.Equal(400_000, x.ExpectedPaise));
        var rejected = await client.PostAsJsonAsync($"/api/groups/{group.Id}/members",
            new AddMemberRequest(Guid.NewGuid(), group.Version, "Later member"));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }

    [Fact]
    public async Task PartialPaymentsDeriveBalancesAndOverdueWithoutChangingExpectedPool()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client);
        var request = new RecordPaymentRequest(Guid.NewGuid(), 40_000, PaidOn, "UPI", "reference");
        (await client.PostAsJsonAsync(PaymentPath(group), request)).EnsureSuccessStatusCode();
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        var cycle = group.Cycles[0];
        Assert.Equal(300_000, cycle.ExpectedPaise);
        Assert.Equal(40_000, cycle.CollectedPaise);
        Assert.Equal(260_000, cycle.OutstandingPaise);
        Assert.Equal(260_000, cycle.PayoutShortfallPaise);
        Assert.Equal("Partial", cycle.Dues[0].Status);
        Assert.True(cycle.Dues[0].IsOverdue);
        Assert.False(group.Cycles[1].Dues[0].IsOverdue);
        (await client.PostAsJsonAsync(PaymentPath(group), request with { OperationId = Guid.NewGuid(), AmountPaise = 60_000 })).EnsureSuccessStatusCode();
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        Assert.Equal("Paid", group.Cycles[0].Dues[0].Status);
        Assert.False(group.Cycles[0].Dues[0].IsOverdue);
    }

    [Fact]
    public async Task RetryReturnsOriginalPaymentAndReusingIdWithDifferentPayloadFails()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client);
        var request = new RecordPaymentRequest(Guid.NewGuid(), 50_000, PaidOn, "Cash", null);
        var original = await client.PostAsJsonAsync(PaymentPath(group), request);
        original.EnsureSuccessStatusCode();
        var retry = await client.PostAsJsonAsync(PaymentPath(group), request);
        retry.EnsureSuccessStatusCode();
        Assert.Equal(await original.Content.ReadFromJsonAsync<MutationResult>(), await retry.Content.ReadFromJsonAsync<MutationResult>());
        var wrongRetry = await client.PostAsJsonAsync(PaymentPath(group), request with { AmountPaise = 20_000 });
        Assert.Equal(HttpStatusCode.Conflict, wrongRetry.StatusCode);
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        Assert.Single(group.Cycles[0].Dues[0].Payments);
        Assert.Equal(50_000, group.Cycles[0].CollectedPaise);
        Assert.Single(group.History, x => x.Kind == "PaymentRecorded");
    }

    [Fact]
    public async Task ConcurrentFullPaymentsNeverOverAllocateTheSameDue()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync(PaymentPath(group),
            new RecordPaymentRequest(Guid.NewGuid(), 100_000, PaidOn, "Cash", null))));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.All(responses.Where(x => !x.IsSuccessStatusCode), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        Assert.Equal(100_000, group.Cycles[0].Dues[0].PaidPaise);
        Assert.Single(group.Cycles[0].Dues[0].Payments);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task ReversalPreservesOriginalRecordAndRestoresOutstandingBalance()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client);
        var paid = await client.PostAsJsonAsync(PaymentPath(group), new RecordPaymentRequest(Guid.NewGuid(), 100_000, PaidOn, "Cash", null));
        paid.EnsureSuccessStatusCode();
        var payment = (await paid.Content.ReadFromJsonAsync<MutationResult>())!;
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        var reverse = new ReversePaymentRequest(Guid.NewGuid(), group.Version, "Entered against the wrong member");
        var path = $"/api/groups/{group.Id}/payments/{payment.EntityId}/reverse";
        (await client.PostAsJsonAsync(path, reverse)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(path, reverse)).EnsureSuccessStatusCode();
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        Assert.Equal(100_000, group.Cycles[0].Dues[0].OutstandingPaise);
        Assert.Equal(0, group.Cycles[0].Dues[0].PaidPaise);
        var original = Assert.Single(group.Cycles[0].Dues[0].Payments);
        Assert.Equal(100_000, original.AmountPaise);
        Assert.Equal(reverse.Reason, original.ReversalReason);
        Assert.Contains(group.History, x => x.Kind == "PaymentRecorded");
        Assert.Single(group.History, x => x.Kind == "PaymentReversed");
    }

    [Fact]
    public async Task StaleCorrectionDoesNotModifyTheLedger()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client);
        var paid = await client.PostAsJsonAsync(PaymentPath(group), new RecordPaymentRequest(Guid.NewGuid(), 40_000, PaidOn, "Cash", null));
        paid.EnsureSuccessStatusCode();
        var result = (await paid.Content.ReadFromJsonAsync<MutationResult>())!;
        var reverse = await client.PostAsJsonAsync($"/api/groups/{group.Id}/payments/{result.EntityId}/reverse",
            new ReversePaymentRequest(Guid.NewGuid(), group.Version, "Wrong amount"));
        Assert.Equal(HttpStatusCode.Conflict, reverse.StatusCode);
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        Assert.Equal(40_000, group.Cycles[0].CollectedPaise);
        Assert.DoesNotContain(group.History, x => x.Kind == "PaymentReversed");
    }

    [Fact]
    public async Task FundingGapNeedsExplanationAndActualPayoutRemainsSeparateFromExpected()
    {
        using var client = await fixture.Organizer();
        var group = await Group(client);
        var request = new RecordPayoutRequest(Guid.NewGuid(), group.Version, 200_000, PaidOn, null);
        var path = $"/api/groups/{group.Id}/cycles/{group.Cycles[0].Id}/payout";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, request)).StatusCode);
        (await client.PostAsJsonAsync(path, request with { FundingNote = "Organizer covered the shortfall externally" })).EnsureSuccessStatusCode();
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        Assert.Equal(300_000, group.Cycles[0].ExpectedPaise);
        var partialPayout = Assert.IsType<PayoutSummary>(group.Cycles[0].Payout);
        Assert.Equal(200_000, partialPayout.AmountPaise);
        Assert.Equal(200_000, group.Cycles[0].PayoutShortfallPaise);
        Assert.Equal(100_000, partialPayout.OutstandingPaise);
        Assert.Single(partialPayout.Entries);
        var remainder = request with { OperationId = Guid.NewGuid(), ExpectedVersion = group.Version, AmountPaise = 100_000, FundingNote = "Balance covered externally" };
        (await client.PostAsJsonAsync(path, remainder)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(path, remainder)).EnsureSuccessStatusCode();
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        var completedPayout = Assert.IsType<PayoutSummary>(group.Cycles[0].Payout);
        Assert.Equal(300_000, completedPayout.AmountPaise);
        Assert.Equal(0, completedPayout.OutstandingPaise);
        Assert.Equal(2, completedPayout.Entries.Length);
        var excess = await client.PostAsJsonAsync(path, remainder with { OperationId = Guid.NewGuid(), ExpectedVersion = group.Version, AmountPaise = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, excess.StatusCode);
    }

    [Fact]
    public async Task OtherOrganizerCannotReadOrWriteGroupAndAnonymousAccessIsRejected()
    {
        using var first = await fixture.Organizer();
        using var second = await fixture.Organizer("secondary");
        using var anonymous = fixture.Factory.CreateClient();
        var group = await Group(first);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/groups")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/api/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.PostAsJsonAsync(PaymentPath(group),
            new RecordPaymentRequest(Guid.NewGuid(), 1, PaidOn, "Cash", null))).StatusCode);
        var others = (await second.GetFromJsonAsync<GroupSummary[]>("/api/groups"))!;
        Assert.DoesNotContain(others, x => x.Id == group.Id);
    }

    [Theory]
    [InlineData(0, "2026-10-12", "Cash", 409)]
    [InlineData(-1, "2026-10-12", "Cash", 409)]
    [InlineData(100_001, "2026-10-12", "Cash", 409)]
    [InlineData(1, "2026-10-14", "Cash", 400)]
    [InlineData(1, "2026-10-12", "Bitcoin", 400)]
    public async Task InvalidPaymentsHaveNoFinancialOrAuditEffects(long amount, string date, string method, int status)
    {
        using var client = await fixture.Organizer();
        var group = await Group(client);
        var response = await client.PostAsJsonAsync(PaymentPath(group), new RecordPaymentRequest(Guid.NewGuid(), amount, DateOnly.Parse(date), method, null));
        Assert.Equal(status, (int)response.StatusCode);
        group = (await client.GetFromJsonAsync<LedgerView>($"/api/groups/{group.Id}"))!;
        Assert.Empty(group.Cycles[0].Dues[0].Payments);
        Assert.Equal(2, group.Version);
        Assert.DoesNotContain(group.History, x => x.Kind == "PaymentRecorded");
    }
}
