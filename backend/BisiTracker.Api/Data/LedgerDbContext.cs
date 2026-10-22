using BisiTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BisiTracker.Api.Data;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<SavingsGroup> Groups => Set<SavingsGroup>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ProcessedOperation> Operations => Set<ProcessedOperation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<SavingsGroup>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(80);
            b.Property(x => x.OwnerId).HasMaxLength(512);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.OwnerId);
            b.HasOne(x => x.Round).WithOne().HasForeignKey<Round>(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Round>(b =>
        {
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Round_Contribution", "\"ContributionPaise\" > 0 AND \"ContributionPaise\" <= 100000000");
                t.HasCheckConstraint("CK_Round_DueDay", "\"DueDay\" BETWEEN 1 AND 28");
            });
            b.HasMany(x => x.Members).WithOne().HasForeignKey(x => x.RoundId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Cycles).WithOne().HasForeignKey(x => x.RoundId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Membership>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(80);
            b.HasIndex(x => new { x.RoundId, x.Position }).IsUnique();
        });
        model.Entity<Cycle>(b =>
        {
            b.HasIndex(x => new { x.RoundId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.RoundId, x.RecipientId }).IsUnique();
            b.HasOne<Membership>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Dues).WithOne().HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Payouts).WithOne().HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ContributionDue>(b =>
        {
            b.ToTable(t => t.HasCheckConstraint("CK_Due_Amount", "\"AmountPaise\" > 0"));
            b.HasIndex(x => new { x.CycleId, x.MemberId }).IsUnique();
            b.HasOne<Membership>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.DueId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PaymentEntry>(b =>
        {
            b.ToTable(t => t.HasCheckConstraint("CK_Payment_Amount", "\"AmountPaise\" > 0"));
            b.Property(x => x.Reference).HasMaxLength(140);
            b.HasOne(x => x.Reversal).WithOne().HasForeignKey<PaymentReversal>(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PaymentReversal>().Property(x => x.Reason).HasMaxLength(300);
        model.Entity<PayoutEntry>(b =>
        {
            b.ToTable(t => t.HasCheckConstraint("CK_Payout_Amount", "\"AmountPaise\" > 0"));
            b.Property(x => x.FundingNote).HasMaxLength(300);
            b.HasOne(x => x.Reversal).WithOne().HasForeignKey<PayoutReversal>(x => x.PayoutId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PayoutReversal>().Property(x => x.Reason).HasMaxLength(300);
        model.Entity<AuditEvent>(b =>
        {
            b.HasIndex(x => new { x.GroupId, x.RecordedAt });
            b.HasOne<SavingsGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ProcessedOperation>(b =>
        {
            b.Property(x => x.OwnerId).HasMaxLength(512);
            b.HasIndex(x => new { x.OwnerId, x.OperationId }).IsUnique();
            b.HasOne<SavingsGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
