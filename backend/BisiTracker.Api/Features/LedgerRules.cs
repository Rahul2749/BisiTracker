using BisiTracker.Api.Domain;

namespace BisiTracker.Api.Features;

public sealed class LedgerException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public static class LedgerRules
{
    public const long MaxContributionPaise = 100_000_000;
    public static readonly string[] Methods = ["Cash", "UPI", "Bank", "Other"];

    public static string Text(string? value, string field, int maxLength)
    {
        var text = value?.Trim() ?? "";
        Require(text.Length > 0 && text.Length <= maxLength, "validation", $"{field} must contain 1–{maxLength} characters.");
        return text;
    }

    public static void Require(bool condition, string code, string message, int statusCode = 400)
    {
        if (!condition) throw new LedgerException(statusCode, code, message);
    }

    public static void Version(SavingsGroup group, long expected) =>
        Require(group.Version == expected, "stale_version", "The group changed. Refresh the ledger before trying again.", 409);

    public static long Collected(ContributionDue due) => due.Payments.Where(x => x.Reversal is null).Sum(x => x.AmountPaise);
    public static long PaidOut(Cycle cycle) => cycle.Payouts.Where(x => x.Reversal is null).Sum(x => x.AmountPaise);
    public static void Open(Cycle cycle) => Require(cycle.ClosedAt is null,
        "cycle_closed", "Reopen this cycle with a reason before changing its records.", 409);

    public static DateOnly Today(TimeProvider time, string zone = "Asia/Kolkata") =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);

    public static void PaymentDate(DateOnly date, TimeProvider time) =>
        Require(date >= new DateOnly(2000, 1, 1) && date <= Today(time), "payment_date", "Use a payment date from 2000 onwards that is not in the future.");

    public static CycleView Cycle(Cycle cycle, Round round, DateOnly today)
    {
        var names = round.Members.ToDictionary(x => x.Id, x => x.Name);
        var positions = round.Members.ToDictionary(x => x.Id, x => x.Position);
        var dues = cycle.Dues.OrderBy(x => positions[x.MemberId]).Select(due =>
        {
            var collected = Collected(due);
            var outstanding = due.AmountPaise - collected;
            return new DueView(due.Id, due.MemberId, names[due.MemberId], due.AmountPaise,
                collected, outstanding, outstanding == 0 ? "Paid" : collected > 0 ? "Partial" : "Pending",
                outstanding > 0 && today > cycle.DueDate,
                due.Payments.OrderByDescending(x => x.RecordedAt).Select(x => new PaymentView(x.Id,
                    x.AmountPaise, x.PaymentDate, x.Method, x.Reference, x.RecordedAt, x.Reversal?.Reason)).ToArray());
        }).ToArray();
        var expected = dues.Sum(x => x.ExpectedPaise);
        var received = dues.Sum(x => x.PaidPaise);
        var paidOut = PaidOut(cycle);
        return new CycleView(cycle.Id, cycle.Number, cycle.DueDate, cycle.RecipientId, names[cycle.RecipientId],
            expected, received, expected - received, Math.Max(0, (cycle.Payouts.Any(x => x.Reversal is null) ? paidOut : expected) - received),
            dues, cycle.Payouts.Count > 0 ? new PayoutSummary(paidOut, expected - paidOut, cycle.Payouts.OrderBy(x => x.RecordedAt)
                .Select(p => new PayoutView(p.Id, p.AmountPaise, p.PaymentDate, p.FundingNote, p.RecordedAt, p.Reversal?.Reason)).ToArray()) : null,
            cycle.ClosedAt);
    }
}
