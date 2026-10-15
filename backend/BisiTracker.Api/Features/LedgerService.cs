using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BisiTracker.Api.Data;
using BisiTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BisiTracker.Api.Features;

public sealed class LedgerService(LedgerDbContext db, TimeProvider time)
{
    private IQueryable<SavingsGroup> Aggregate => db.Groups
        .Include(x => x.Round).ThenInclude(x => x.Members)
        .Include(x => x.Round).ThenInclude(x => x.Cycles).ThenInclude(x => x.Dues).ThenInclude(x => x.Payments).ThenInclude(x => x.Reversal)
        .Include(x => x.Round).ThenInclude(x => x.Cycles).ThenInclude(x => x.Payouts).AsSplitQuery();

    private async Task<SavingsGroup> Owned(string owner, Guid id, CancellationToken ct) =>
        await Aggregate.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct)
        ?? throw new LedgerException(404, "not_found", "Group not found.");

    public async Task<GroupSummary[]> List(string owner, int page, int pageSize, CancellationToken ct)
    {
        LedgerRules.Require(page is >= 1 and <= 10_000 && pageSize is >= 1 and <= 50,
            "pagination", "Page must be 1–10000 and page size 1–50.");
        return await db.Groups.AsNoTracking().Where(x => x.OwnerId == owner)
            .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new GroupSummary(x.Id, x.Name, x.Status, x.Version, x.Language,
                x.Round.ContributionPaise, x.Round.Members.Count, x.Round.StartMonth, x.UpdatedAt)).ToArrayAsync(ct);
    }

    public async Task<LedgerView> Get(string owner, Guid id, CancellationToken ct)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var group = await Owned(owner, id, ct);
        var history = await db.AuditEvents.AsNoTracking().Where(x => x.GroupId == id)
            .OrderByDescending(x => x.RecordedAt).ThenBy(x => x.Id).Take(200)
            .Select(x => new AuditView(x.Id, x.Kind, x.Description, x.RecordedAt)).ToArrayAsync(ct);
        var view = new LedgerView(group.Id, group.Name, group.Status, group.Version, group.Currency, group.Language,
            group.Round.ContributionPaise, group.Round.DueDay, group.Round.StartMonth, group.UpdatedAt,
            group.Round.Members.OrderBy(x => x.Position).Select(x => new MemberView(x.Id, x.Name, x.Position)).ToArray(),
            group.Round.Cycles.OrderBy(x => x.Number).Select(x => LedgerRules.Cycle(x, group.Round,
                LedgerRules.Today(time, group.TimeZone))).ToArray(), history);
        await snapshot.CommitAsync(ct);
        return view;
    }

    private async Task<MutationResult> Execute(string owner, Guid operationId, string kind, object payload,
        Func<Task<(SavingsGroup Group, Guid? EntityId)>> action, CancellationToken ct)
    {
        LedgerRules.Require(operationId != Guid.Empty, "operation_id", "A non-empty operation ID is required.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { kind, payload }))));
        var existing = await db.Operations.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.OperationId == operationId, ct);
        if (existing is not null)
        {
            LedgerRules.Require(existing.RequestHash == hash, "operation_reused", "This operation ID was already used for a different request.", 409);
            return new MutationResult(existing.GroupId, existing.EntityId);
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (group, entityId) = await action();
        group.UpdatedAt = time.GetUtcNow();
        db.Operations.Add(new ProcessedOperation
        {
            OwnerId = owner, OperationId = operationId, RequestHash = hash, GroupId = group.Id,
            EntityId = entityId, RecordedAt = time.GetUtcNow()
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new MutationResult(group.Id, entityId);
    }

    private void Audit(SavingsGroup group, string owner, string kind, string description, Guid? cycleId = null)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            GroupId = group.Id, CycleId = cycleId, ActorId = owner, Kind = kind,
            Description = description, RecordedAt = time.GetUtcNow()
        });
    }

    public Task<MutationResult> Create(string owner, CreateGroupRequest request, CancellationToken ct) =>
        Execute(owner, request.OperationId, "create_group", request, () =>
        {
            var name = LedgerRules.Text(request.Name, "Group name", 80);
            LedgerRules.Require(request.ContributionPaise is > 0 and <= LedgerRules.MaxContributionPaise,
                "contribution", "Contribution must be between ₹0.01 and ₹10,00,000.");
            LedgerRules.Require(request.DueDay is >= 1 and <= 28, "due_day", "Due day must be 1–28.");
            LedgerRules.Require(request.StartMonth.Day == 1 && request.StartMonth.Year is >= 2000 and <= 2090,
                "start_month", "Start month must be the first day of a month between 2000 and 2090.");
            LedgerRules.Require(request.Language is "en" or "mr", "language", "Choose English or Marathi.");
            LedgerRules.Require(request.MemberNames is not null && request.MemberNames.Length <= 50,
                "members", "A group can have at most 50 members.");
            var group = new SavingsGroup { OwnerId = owner, Name = name, Language = request.Language };
            group.Round = new Round
            {
                GroupId = group.Id, ContributionPaise = request.ContributionPaise,
                DueDay = request.DueDay, StartMonth = request.StartMonth
            };
            foreach (var member in request.MemberNames!)
                group.Round.Members.Add(new Membership
                {
                    RoundId = group.Round.Id, Name = LedgerRules.Text(member, "Member name", 80),
                    Position = group.Round.Members.Count + 1
                });
            db.Groups.Add(group);
            Audit(group, owner, "GroupCreated", "Group created as a draft.");
            return Task.FromResult<(SavingsGroup, Guid?)>((group, group.Id));
        }, ct);

    public Task<MutationResult> AddMember(string owner, Guid id, AddMemberRequest request, CancellationToken ct) =>
        Execute(owner, request.OperationId, "add_member", new { id, request }, async () =>
        {
            var group = await Owned(owner, id, ct);
            LedgerRules.Version(group, request.ExpectedVersion);
            LedgerRules.Require(group.Status == "Draft", "rules_frozen", "Members are fixed after a round starts.", 409);
            LedgerRules.Require(group.Round.Members.Count < 50, "members", "A group can have at most 50 members.");
            var member = new Membership
            {
                RoundId = group.Round.Id, Name = LedgerRules.Text(request.Name, "Member name", 80),
                Position = group.Round.Members.Count + 1
            };
            group.Round.Members.Add(member);
            db.Add(member);
            group.Version++;
            Audit(group, owner, "MemberAdded", $"{member.Name} added in payout position {member.Position}.");
            return (group, member.Id);
        }, ct);

    public Task<MutationResult> Activate(string owner, Guid id, ActivateRequest request, CancellationToken ct) =>
        Execute(owner, request.OperationId, "activate", new { id, request }, async () =>
        {
            var group = await Owned(owner, id, ct);
            LedgerRules.Version(group, request.ExpectedVersion);
            LedgerRules.Require(group.Status == "Draft", "already_active", "This round has already started.", 409);
            LedgerRules.Require(group.Round.Members.Count >= 2, "members", "Add at least two members before starting.");
            foreach (var member in group.Round.Members.OrderBy(x => x.Position))
            {
                var month = group.Round.StartMonth.AddMonths(member.Position - 1);
                var cycle = new Cycle
                {
                    RoundId = group.Round.Id, Number = member.Position,
                    DueDate = new DateOnly(month.Year, month.Month, group.Round.DueDay), RecipientId = member.Id
                };
                cycle.Dues = group.Round.Members.Select(x => new ContributionDue
                {
                    CycleId = cycle.Id, MemberId = x.Id, AmountPaise = group.Round.ContributionPaise
                }).ToList();
                group.Round.Cycles.Add(cycle);
                db.Add(cycle);
            }
            group.Status = "Active";
            group.Version++;
            Audit(group, owner, "RoundActivated", "Round started. Contribution, members and payout order are fixed.");
            return (group, null);
        }, ct);

    public Task<MutationResult> Payment(string owner, Guid id, Guid cycleId, Guid memberId,
        RecordPaymentRequest request, CancellationToken ct) =>
        Execute(owner, request.OperationId, "payment", new { id, cycleId, memberId, request }, async () =>
        {
            var group = await Owned(owner, id, ct);
            var cycle = group.Round.Cycles.SingleOrDefault(x => x.Id == cycleId)
                ?? throw new LedgerException(404, "not_found", "Cycle not found.");
            var due = cycle.Dues.SingleOrDefault(x => x.MemberId == memberId)
                ?? throw new LedgerException(404, "not_found", "Member not found in this cycle.");
            LedgerRules.Require(request.AmountPaise > 0 && request.AmountPaise <= due.AmountPaise - LedgerRules.Collected(due),
                "overpayment", "Amount must be positive and no greater than this member's outstanding contribution.", 409);
            LedgerRules.PaymentDate(request.PaymentDate, time);
            LedgerRules.Require(LedgerRules.Methods.Contains(request.Method), "method", "Choose Cash, UPI, Bank or Other.");
            LedgerRules.Require(request.Reference is null || request.Reference.Length <= 140, "reference", "Reference must be at most 140 characters.");
            var payment = new PaymentEntry
            {
                DueId = due.Id, AmountPaise = request.AmountPaise, PaymentDate = request.PaymentDate,
                Method = request.Method, Reference = request.Reference?.Trim(), ActorId = owner, RecordedAt = time.GetUtcNow()
            };
            due.Payments.Add(payment);
            db.Add(payment);
            group.Version++;
            var name = group.Round.Members.Single(x => x.Id == memberId).Name;
            Audit(group, owner, "PaymentRecorded", $"{name}: ₹{request.AmountPaise / 100m:0.00} recorded by {request.Method} for cycle {cycle.Number}.", cycleId);
            return (group, payment.Id);
        }, ct);

    public Task<MutationResult> Reverse(string owner, Guid id, Guid paymentId,
        ReversePaymentRequest request, CancellationToken ct) =>
        Execute(owner, request.OperationId, "reverse", new { id, paymentId, request }, async () =>
        {
            var group = await Owned(owner, id, ct);
            LedgerRules.Version(group, request.ExpectedVersion);
            var cycle = group.Round.Cycles.SingleOrDefault(x => x.Dues.Any(d => d.Payments.Any(p => p.Id == paymentId)))
                ?? throw new LedgerException(404, "not_found", "Payment not found.");
            var payment = cycle.Dues.SelectMany(x => x.Payments).Single(x => x.Id == paymentId);
            LedgerRules.Require(payment.Reversal is null, "already_reversed", "This payment was already reversed.", 409);
            var reversal = new PaymentReversal
            {
                PaymentId = payment.Id, Reason = LedgerRules.Text(request.Reason, "Correction reason", 300),
                ActorId = owner, RecordedAt = time.GetUtcNow()
            };
            payment.Reversal = reversal;
            db.Add(reversal);
            group.Version++;
            Audit(group, owner, "PaymentReversed", $"₹{payment.AmountPaise / 100m:0.00} reversed: {reversal.Reason}", cycle.Id);
            return (group, reversal.Id);
        }, ct);

    public Task<MutationResult> Payout(string owner, Guid id, Guid cycleId, RecordPayoutRequest request, CancellationToken ct) =>
        Execute(owner, request.OperationId, "payout", new { id, cycleId, request }, async () =>
        {
            var group = await Owned(owner, id, ct);
            LedgerRules.Version(group, request.ExpectedVersion);
            var cycle = group.Round.Cycles.SingleOrDefault(x => x.Id == cycleId)
                ?? throw new LedgerException(404, "not_found", "Cycle not found.");
            var expected = cycle.Dues.Sum(x => x.AmountPaise);
            var collected = cycle.Dues.Sum(LedgerRules.Collected);
            var paidOut = cycle.Payouts.Sum(x => x.AmountPaise);
            LedgerRules.Require(request.AmountPaise > 0 && request.AmountPaise <= expected - paidOut,
                "payout_amount", "Payout must be positive and no greater than the remaining payout entitlement.");
            LedgerRules.PaymentDate(request.PaymentDate, time);
            LedgerRules.Require(request.FundingNote is null || request.FundingNote.Length <= 300, "funding_note", "Funding note must be at most 300 characters.");
            LedgerRules.Require(paidOut + request.AmountPaise <= collected || !string.IsNullOrWhiteSpace(request.FundingNote),
                "funding_gap", "Explain how the payout was funded when it exceeds recorded contributions.");
            var payout = new PayoutEntry
            {
                CycleId = cycleId, AmountPaise = request.AmountPaise, PaymentDate = request.PaymentDate,
                FundingNote = request.FundingNote?.Trim(), ActorId = owner, RecordedAt = time.GetUtcNow()
            };
            cycle.Payouts.Add(payout);
            db.Add(payout);
            group.Version++;
            var name = group.Round.Members.Single(x => x.Id == cycle.RecipientId).Name;
            Audit(group, owner, "PayoutRecorded", $"₹{request.AmountPaise / 100m:0.00} payout recorded for {name} in cycle {cycle.Number}.", cycleId);
            return (group, payout.Id);
        }, ct);
}
