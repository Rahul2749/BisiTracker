using BisiTracker.Api.Features;

namespace BisiTracker.Tests;

public sealed class LedgerRulesTests
{
    [Fact]
    public void DueDateUsesIndianDateAcrossUtcMidnightBoundary()
    {
        Assert.Equal(new DateOnly(2026, 10, 14), LedgerRules.Today(new BoundaryClock()));
    }

    private sealed class BoundaryClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 13, 20, 0, 0, TimeSpan.Zero);
    }
}
