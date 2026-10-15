namespace BisiTracker.Api.Domain;

public sealed class SavingsGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Currency { get; set; } = "INR";
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string Language { get; set; } = "en";
    public string Status { get; set; } = "Draft";
    public long Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Round Round { get; set; } = null!;
}

public sealed class Round
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public long ContributionPaise { get; set; }
    public DateOnly StartMonth { get; set; }
    public int DueDay { get; set; }
    public List<Membership> Members { get; set; } = [];
    public List<Cycle> Cycles { get; set; } = [];
}

public sealed class Membership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoundId { get; set; }
    public string Name { get; set; } = "";
    public int Position { get; set; }
}

public sealed class Cycle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoundId { get; set; }
    public int Number { get; set; }
    public DateOnly DueDate { get; set; }
    public Guid RecipientId { get; set; }
    public List<ContributionDue> Dues { get; set; } = [];
    public List<PayoutEntry> Payouts { get; set; } = [];
}

public sealed class ContributionDue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public Guid MemberId { get; set; }
    public long AmountPaise { get; set; }
    public List<PaymentEntry> Payments { get; set; } = [];
}

public sealed class PaymentEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DueId { get; set; }
    public long AmountPaise { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string Method { get; set; } = "Cash";
    public string? Reference { get; set; }
    public string ActorId { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public PaymentReversal? Reversal { get; set; }
}

public sealed class PaymentReversal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public string Reason { get; set; } = "";
    public string ActorId { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PayoutEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CycleId { get; set; }
    public long AmountPaise { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? FundingNote { get; set; }
    public string ActorId { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid? CycleId { get; set; }
    public string ActorId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProcessedOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public Guid OperationId { get; set; }
    public string RequestHash { get; set; } = "";
    public Guid GroupId { get; set; }
    public Guid? EntityId { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}
