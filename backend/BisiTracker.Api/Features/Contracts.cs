namespace BisiTracker.Api.Features;

public sealed record CreateGroupRequest(Guid OperationId, string Name, long ContributionPaise,
    DateOnly StartMonth, int DueDay, string Language, string[] MemberNames);
public sealed record AddMemberRequest(Guid OperationId, long ExpectedVersion, string Name);
public sealed record DraftMemberEdit(Guid Id, string Name);
public sealed record EditMembersRequest(Guid OperationId, long ExpectedVersion, DraftMemberEdit[] Members);
public sealed record ReversePayoutRequest(Guid OperationId, long ExpectedVersion, string Reason);
public sealed record CloseCycleRequest(Guid OperationId, long ExpectedVersion);
public sealed record ReopenCycleRequest(Guid OperationId, long ExpectedVersion, string Reason);
public sealed record ActivateRequest(Guid OperationId, long ExpectedVersion);
public sealed record RecordPaymentRequest(Guid OperationId, long AmountPaise,
    DateOnly PaymentDate, string Method, string? Reference);
public sealed record ReversePaymentRequest(Guid OperationId, long ExpectedVersion, string Reason);
public sealed record RecordPayoutRequest(Guid OperationId, long ExpectedVersion, long AmountPaise,
    DateOnly PaymentDate, string? FundingNote);
public sealed record GroupSummary(Guid Id, string Name, string Status, long Version, string Language,
    long ContributionPaise, int MemberCount, DateOnly StartMonth, DateTimeOffset UpdatedAt);
public sealed record MemberView(Guid Id, string Name, int Position);
public sealed record PaymentView(Guid Id, long AmountPaise, DateOnly PaymentDate, string Method,
    string? Reference, DateTimeOffset RecordedAt, string? ReversalReason);
public sealed record DueView(Guid Id, Guid MemberId, string Name, long ExpectedPaise,
    long PaidPaise, long OutstandingPaise, string Status, bool IsOverdue, PaymentView[] Payments);
public sealed record PayoutView(Guid Id, long AmountPaise, DateOnly PaymentDate, string? FundingNote,
    DateTimeOffset RecordedAt, string? ReversalReason);
public sealed record PayoutSummary(long AmountPaise, long OutstandingPaise, PayoutView[] Entries);
public sealed record CycleView(Guid Id, int Number, DateOnly DueDate, Guid RecipientId,
    string RecipientName, long ExpectedPaise, long CollectedPaise, long OutstandingPaise,
    long PayoutShortfallPaise, DueView[] Dues, PayoutSummary? Payout, DateTimeOffset? ClosedAt);
public sealed record AuditView(Guid Id, string Kind, string Description, DateTimeOffset RecordedAt);
public sealed record AuditPage(AuditView[] Items, int? NextPage, long Version);
public sealed record LedgerView(Guid Id, string Name, string Status, long Version, string Currency,
    string Language, long ContributionPaise, int DueDay, DateOnly StartMonth,
    DateTimeOffset UpdatedAt, MemberView[] Members, CycleView[] Cycles, AuditView[] History, int? HistoryNextPage);
public sealed record MutationResult(Guid GroupId, Guid? EntityId);
