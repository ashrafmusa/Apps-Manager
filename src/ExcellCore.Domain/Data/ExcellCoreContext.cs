using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Data;

public sealed class ExcellCoreContext : DbContext
{
    public ExcellCoreContext(DbContextOptions<ExcellCoreContext> options)
        : base(options)
    {
    }

    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyIdentifier> PartyIdentifiers => Set<PartyIdentifier>();
    public DbSet<Agreement> Agreements => Set<Agreement>();
    public DbSet<AgreementRate> AgreementRates => Set<AgreementRate>();
    public DbSet<AgreementApproval> AgreementApprovals => Set<AgreementApproval>();
    public DbSet<AgreementImpactedParty> AgreementImpactedParties => Set<AgreementImpactedParty>();
    public DbSet<ActionLog> ActionLogs => Set<ActionLog>();
    public DbSet<TelemetryEvent> TelemetryEvents => Set<TelemetryEvent>();
    public DbSet<TelemetryAggregate> TelemetryAggregates => Set<TelemetryAggregate>();
    public DbSet<TelemetryThreshold> TelemetryThresholds => Set<TelemetryThreshold>();
    public DbSet<TelemetryHealthSnapshot> TelemetryHealthSnapshots => Set<TelemetryHealthSnapshot>();
    public DbSet<RetailTransaction> RetailTransactions => Set<RetailTransaction>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<MedicationOrder> MedicationOrders => Set<MedicationOrder>();
    public DbSet<MedicationAdministration> MedicationAdministrations => Set<MedicationAdministration>();
    public DbSet<DispenseEvent> DispenseEvents => Set<DispenseEvent>();
    public DbSet<RetailSuspendedTransaction> RetailSuspendedTransactions => Set<RetailSuspendedTransaction>();
    public DbSet<RetailReturn> RetailReturns => Set<RetailReturn>();
    public DbSet<CorporateContract> CorporateContracts => Set<CorporateContract>();
    public DbSet<ReportingDashboard> ReportingDashboards => Set<ReportingDashboard>();
    public DbSet<ReportingSchedule> ReportingSchedules => Set<ReportingSchedule>();
    public DbSet<SyncChangeLedgerEntry> SyncChangeLedgerEntries => Set<SyncChangeLedgerEntry>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<PartyMetadata> PartyMetadata => Set<PartyMetadata>();
    public DbSet<InventoryLedgerEntry> InventoryLedgerEntries => Set<InventoryLedgerEntry>();
    public DbSet<InventoryAnomalyAlert> InventoryAnomalyAlerts => Set<InventoryAnomalyAlert>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<OrderResult> OrderResults => Set<OrderResult>();
    public DbSet<OrderSet> OrderSets => Set<OrderSet>();
    public DbSet<Ward> Wards => Set<Ward>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Bed> Beds => Set<Bed>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Party>(builder =>
        {
            builder.HasKey(p => p.PartyId);
            builder.Property(p => p.PartyType).HasMaxLength(64);
            builder.Property(p => p.DisplayName).HasMaxLength(256);
            builder.HasMany(p => p.Identifiers)
                .WithOne()
                .HasForeignKey(pi => pi.PartyId);
            builder.OwnsOne(p => p.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<PartyIdentifier>(builder =>
        {
            builder.HasKey(pi => pi.PartyIdentifierId);
            builder.Property(pi => pi.Scheme).HasMaxLength(64);
            builder.Property(pi => pi.Value).HasMaxLength(128);
            builder.OwnsOne(pi => pi.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<Agreement>(builder =>
        {
            builder.HasKey(a => a.AgreementId);
            builder.Property(a => a.AgreementName).HasMaxLength(256);
            builder.Property(a => a.PayerName).HasMaxLength(256);
            builder.Property(a => a.CoverageType).HasMaxLength(128);
            builder.Property(a => a.Status).HasMaxLength(64).HasDefaultValue(AgreementStatus.Draft);
            builder.Property(a => a.RenewalDate);
            builder.Property(a => a.LastRenewedOn);
            builder.Property(a => a.RequiresApproval);
            builder.Property(a => a.AutoRenew);
            builder.HasMany(a => a.Rates)
                .WithOne()
                .HasForeignKey(r => r.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(a => a.Approvals)
                .WithOne()
                .HasForeignKey(ap => ap.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(a => a.ImpactedParties)
                .WithOne()
                .HasForeignKey(ip => ip.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.OwnsOne(a => a.Audit, audit =>
            {
                audit.Property(p => p.CreatedBy).HasMaxLength(64);
                audit.Property(p => p.ModifiedBy).HasMaxLength(64);
                audit.Property(p => p.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<AgreementRate>(builder =>
        {
            builder.HasKey(r => r.AgreementRateId);
            builder.Property(r => r.ServiceCode).HasMaxLength(128);
            builder.Property(r => r.BaseAmount).HasColumnType("decimal(18,2)");
            builder.Property(r => r.DiscountPercent).HasColumnType("decimal(5,2)");
            builder.Property(r => r.CopayPercent).HasColumnType("decimal(5,2)");
            builder.OwnsOne(r => r.Audit, audit =>
            {
                audit.Property(p => p.CreatedBy).HasMaxLength(64);
                audit.Property(p => p.ModifiedBy).HasMaxLength(64);
                audit.Property(p => p.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<AgreementApproval>(builder =>
        {
            builder.HasKey(a => a.AgreementApprovalId);
            builder.Property(a => a.Approver).HasMaxLength(256);
            builder.Property(a => a.Decision).HasMaxLength(32);
            builder.Property(a => a.Comments).HasMaxLength(1024);
            builder.Property(a => a.RequestedOnUtc);
            builder.Property(a => a.DecidedOnUtc);
            builder.Property(a => a.EscalatedOnUtc);
            builder.OwnsOne(a => a.Audit, audit =>
            {
                audit.Property(p => p.CreatedBy).HasMaxLength(64);
                audit.Property(p => p.ModifiedBy).HasMaxLength(64);
                audit.Property(p => p.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<AgreementImpactedParty>(builder =>
        {
            builder.HasKey(p => p.AgreementImpactedPartyId);
            builder.Property(p => p.Relationship).HasMaxLength(64);
            builder.Property(p => p.AgreementId).IsRequired();
            builder.Property(p => p.PartyId).IsRequired();
            builder.HasIndex(p => new { p.AgreementId, p.PartyId }).IsUnique();
            builder.HasIndex(p => p.PartyId);
            builder.HasOne<Party>()
                .WithMany()
                .HasForeignKey(p => p.PartyId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.OwnsOne(p => p.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<ActionLog>(builder =>
        {
            builder.HasKey(l => l.ActionLogId);
            builder.Property(l => l.ActionType).HasMaxLength(64);
            builder.Property(l => l.PerformedBy).HasMaxLength(64);
            builder.Property(l => l.Notes).HasMaxLength(1024);
            builder.Property(l => l.ActionedOnUtc).IsRequired();
            builder.HasIndex(l => l.AgreementId);
            builder.HasIndex(l => l.AgreementApprovalId);
            builder.HasOne<Agreement>()
                .WithMany()
                .HasForeignKey(l => l.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<AgreementApproval>()
                .WithMany()
                .HasForeignKey(l => l.AgreementApprovalId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.OwnsOne(l => l.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<TelemetryEvent>(builder =>
        {
            builder.HasKey(t => t.TelemetryEventId);
            builder.Property(t => t.EventType).HasMaxLength(128);
            builder.Property(t => t.CommandText).HasColumnType("TEXT");
            builder.Property(t => t.DurationMilliseconds).IsRequired();
            builder.Property(t => t.OccurredOnUtc).IsRequired();
            builder.OwnsOne(t => t.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<TelemetryAggregate>(builder =>
        {
            builder.HasKey(t => t.TelemetryAggregateId);
            builder.Property(t => t.MetricKey).HasMaxLength(128);
            builder.Property(t => t.PeriodStartUtc).IsRequired();
            builder.Property(t => t.PeriodEndUtc).IsRequired();
            builder.Property(t => t.AverageDurationMs);
            builder.Property(t => t.MaxDurationMs);
            builder.Property(t => t.P95DurationMs);
            builder.HasIndex(t => new { t.MetricKey, t.PeriodStartUtc });
            builder.OwnsOne(t => t.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<TelemetryThreshold>(builder =>
        {
            builder.HasKey(t => t.TelemetryThresholdId);
            builder.Property(t => t.MetricKey).HasMaxLength(128);
            builder.Property(t => t.WarningThresholdMs);
            builder.Property(t => t.CriticalThresholdMs);
            builder.HasIndex(t => t.MetricKey).IsUnique();
            builder.OwnsOne(t => t.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<TelemetryHealthSnapshot>(builder =>
        {
            builder.HasKey(t => t.TelemetryHealthSnapshotId);
            builder.Property(t => t.MetricKey).HasMaxLength(128);
            builder.Property(t => t.Status).HasMaxLength(32);
            builder.Property(t => t.Message).HasMaxLength(512);
            builder.Property(t => t.CapturedOnUtc).IsRequired();
            builder.HasIndex(t => new { t.MetricKey, t.CapturedOnUtc });
            builder.OwnsOne(t => t.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<RetailTransaction>(builder =>
        {
            builder.HasKey(r => r.RetailTransactionId);
            builder.Property(r => r.TicketNumber).HasMaxLength(64);
            builder.Property(r => r.Channel).HasMaxLength(64);
            builder.Property(r => r.Status).HasMaxLength(64);
            builder.Property(r => r.TotalAmount).HasColumnType("decimal(18,2)");
            builder.Property(r => r.OccurredOnUtc).IsRequired();
            builder.HasMany(r => r.Tickets)
                .WithOne(t => t.RetailTransaction)
                .HasForeignKey(t => t.RetailTransactionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.OwnsOne(r => r.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<Ticket>(builder =>
        {
            builder.HasKey(t => t.TicketId);
            builder.Property(t => t.TicketNumber).HasMaxLength(64);
            builder.Property(t => t.Title).HasMaxLength(256);
            builder.Property(t => t.Status).HasMaxLength(64);
            builder.Property(t => t.Channel).HasMaxLength(64);
            builder.Property(t => t.RaisedOnUtc).IsRequired();
            builder.HasIndex(t => new { t.RetailTransactionId, t.TicketNumber });
            builder.OwnsOne(t => t.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<MedicationOrder>(builder =>
        {
            builder.HasKey(o => o.MedicationOrderId);
            builder.Property(o => o.OrderNumber).HasMaxLength(64);
            builder.Property(o => o.Medication).HasMaxLength(256);
            builder.Property(o => o.Dose).HasMaxLength(128);
            builder.Property(o => o.Route).HasMaxLength(64);
            builder.Property(o => o.Frequency).HasMaxLength(128);
            builder.Property(o => o.Status).HasMaxLength(64);
            builder.Property(o => o.OrderingProvider).HasMaxLength(256);
            builder.Property(o => o.Notes).HasMaxLength(1024);
            builder.HasOne<Party>()
                .WithMany()
                .HasForeignKey(o => o.PartyId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(o => o.Administrations)
                .WithOne()
                .HasForeignKey(a => a.MedicationOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(o => o.DispenseEvents)
                .WithOne()
                .HasForeignKey(d => d.MedicationOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(o => o.OrderNumber).IsUnique();
            builder.OwnsOne(o => o.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<MedicationAdministration>(builder =>
        {
            builder.HasKey(a => a.MedicationAdministrationId);
            builder.Property(a => a.Status).HasMaxLength(64);
            builder.Property(a => a.PerformedBy).HasMaxLength(256);
            builder.Property(a => a.Notes).HasMaxLength(1024);
            builder.HasIndex(a => new { a.MedicationOrderId, a.ScheduledForUtc });
            builder.OwnsOne(a => a.Audit, audit =>
            {
                audit.Property(p => p.CreatedBy).HasMaxLength(64);
                audit.Property(p => p.ModifiedBy).HasMaxLength(64);
                audit.Property(p => p.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<DispenseEvent>(builder =>
        {
            builder.HasKey(d => d.DispenseEventId);
            builder.Property(d => d.InventoryItem).HasMaxLength(128);
            builder.Property(d => d.Unit).HasMaxLength(32);
            builder.Property(d => d.DispensedOnUtc).IsRequired();
            builder.Property(d => d.DispensedBy).HasMaxLength(128);
            builder.Property(d => d.Location).HasMaxLength(128);
            builder.HasIndex(d => new { d.MedicationOrderId, d.DispensedOnUtc });
            builder.OwnsOne(d => d.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<RetailSuspendedTransaction>(builder =>
        {
            builder.HasKey(t => t.RetailSuspendedTransactionId);
            builder.Property(t => t.TicketNumber).HasMaxLength(64);
            builder.Property(t => t.Channel).HasMaxLength(64);
            builder.Property(t => t.Status).HasMaxLength(64);
            builder.Property(t => t.Subtotal).HasColumnType("decimal(18,2)");
            builder.Property(t => t.Notes).HasMaxLength(1024);
            builder.Property(t => t.PayloadJson).HasColumnType("TEXT");
            builder.Property(t => t.SuspendedOnUtc).IsRequired();
            builder.HasIndex(t => t.TicketNumber);
            builder.OwnsOne(t => t.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<RetailReturn>(builder =>
        {
            builder.HasKey(r => r.RetailReturnId);
            builder.Property(r => r.TicketNumber).HasMaxLength(64);
            builder.Property(r => r.Channel).HasMaxLength(64);
            builder.Property(r => r.Status).HasMaxLength(64);
            builder.Property(r => r.Reason).HasMaxLength(256);
            builder.Property(r => r.Amount).HasColumnType("decimal(18,2)");
            builder.Property(r => r.ReturnedOnUtc).IsRequired();
            builder.HasIndex(r => new { r.RetailTransactionId, r.TicketNumber });
            builder.OwnsOne(r => r.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<CorporateContract>(builder =>
        {
            builder.HasKey(c => c.CorporateContractId);
            builder.Property(c => c.ContractCode).HasMaxLength(64);
            builder.Property(c => c.CustomerName).HasMaxLength(256);
            builder.Property(c => c.Category).HasMaxLength(64);
            builder.Property(c => c.Program).HasMaxLength(128);
            builder.Property(c => c.AllocationStatus).HasMaxLength(64);
            builder.Property(c => c.ContractValue).HasColumnType("decimal(18,2)");
            builder.Property(c => c.AllocationRatio).HasColumnType("decimal(6,3)");
            builder.Property(c => c.RenewalDate).IsRequired();
            builder.OwnsOne(c => c.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<ReportingDashboard>(builder =>
        {
            builder.HasKey(d => d.ReportingDashboardId);
            builder.Property(d => d.Name).HasMaxLength(256);
            builder.Property(d => d.Description).HasMaxLength(512);
            builder.Property(d => d.Domain).HasMaxLength(128);
            builder.Property(d => d.IsActive).HasDefaultValue(true);
            builder.OwnsOne(d => d.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<ReportingSchedule>(builder =>
        {
            builder.HasKey(s => s.ReportingScheduleId);
            builder.Property(s => s.Name).HasMaxLength(256);
            builder.Property(s => s.Format).HasMaxLength(64);
            builder.Property(s => s.IsEnabled).HasDefaultValue(true);
            builder.Property(s => s.Cadence);
            builder.Property(s => s.NextRunUtc);
            builder.OwnsOne(s => s.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<PartyMetadata>(builder =>
        {
            builder.HasKey(p => p.PartyMetadataId);
            builder.Property(p => p.Context).HasMaxLength(128);
            builder.Property(p => p.Key).HasMaxLength(128);
            builder.Property(p => p.Value).HasMaxLength(1024);
            builder.HasIndex(p => new { p.PartyId, p.Context, p.Key }).IsUnique();
            builder.HasOne<Party>()
                .WithMany()
                .HasForeignKey(p => p.PartyId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.OwnsOne(p => p.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<SyncChangeLedgerEntry>(builder =>
        {
            builder.HasKey(e => e.SyncChangeLedgerEntryId);
            builder.Property(e => e.AggregateType).HasMaxLength(128);
            builder.Property(e => e.FieldName).HasMaxLength(128);
            builder.Property(e => e.OriginSiteId).HasMaxLength(128);
            builder.Property(e => e.OriginDeviceId).HasMaxLength(128);
            builder.Property(e => e.VectorClockJson).HasColumnType("TEXT");
            builder.Property(e => e.ObservedOnUtc).IsRequired();
            builder.HasIndex(e => new { e.AggregateType, e.AggregateId, e.ObservedOnUtc });
            builder.OwnsOne(e => e.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<SyncState>(builder =>
        {
            builder.HasKey(s => s.SyncStateId);
            builder.Property(s => s.AggregateType).HasMaxLength(128);
            builder.Property(s => s.VectorClockJson).HasColumnType("TEXT");
            builder.Property(s => s.UpdatedOnUtc).IsRequired();
            builder.HasIndex(s => new { s.AggregateType, s.AggregateId }).IsUnique();
            builder.OwnsOne(s => s.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<InventoryLedgerEntry>(builder =>
        {
            builder.HasKey(e => e.InventoryLedgerEntryId);
            builder.Property(e => e.Sku).HasMaxLength(64);
            builder.Property(e => e.ItemName).HasMaxLength(256);
            builder.Property(e => e.Location).HasMaxLength(128);
            builder.Property(e => e.SourceReference).HasMaxLength(128);
            builder.Property(e => e.QuantityDelta).HasColumnType("decimal(18,2)");
            builder.Property(e => e.QuantityOnHand).HasColumnType("decimal(18,2)");
            builder.Property(e => e.ReorderPoint).HasColumnType("decimal(18,2)");
            builder.Property(e => e.OnOrder).HasColumnType("decimal(18,2)");
            builder.Property(e => e.OccurredOnUtc).IsRequired();
            builder.HasIndex(e => new { e.Sku, e.Location, e.OccurredOnUtc });
            builder.OwnsOne(e => e.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<InventoryAnomalyAlert>(builder =>
        {
            builder.HasKey(a => a.InventoryAnomalyAlertId);
            builder.Property(a => a.Sku).HasMaxLength(64);
            builder.Property(a => a.ItemName).HasMaxLength(256);
            builder.Property(a => a.Location).HasMaxLength(128);
            builder.Property(a => a.SignalType).HasMaxLength(64);
            builder.Property(a => a.Severity).HasMaxLength(32);
            builder.Property(a => a.Message).HasMaxLength(512);
            builder.Property(a => a.ReorderPoint).HasColumnType("decimal(18,2)");
            builder.Property(a => a.QuantityOnHand).HasColumnType("decimal(18,2)");
            builder.Property(a => a.DetectedOnUtc).IsRequired();
            builder.Property(a => a.WindowStartUtc).IsRequired();
            builder.Property(a => a.WindowEndUtc).IsRequired();
            builder.HasIndex(a => new { a.Sku, a.Location, a.DetectedOnUtc });
            builder.OwnsOne(a => a.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<Ward>(builder =>
        {
            builder.HasKey(w => w.WardId);
            builder.Property(w => w.Name).HasMaxLength(128);
            builder.Property(w => w.Code).HasMaxLength(32);
            builder.HasMany(w => w.Rooms)
                .WithOne(r => r.Ward)
                .HasForeignKey(r => r.WardId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.OwnsOne(w => w.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<Room>(builder =>
        {
            builder.HasKey(r => r.RoomId);
            builder.Property(r => r.RoomNumber).HasMaxLength(32);
            builder.HasIndex(r => new { r.WardId, r.RoomNumber }).IsUnique();
            builder.HasOne(r => r.Ward)
                .WithMany(w => w.Rooms)
                .HasForeignKey(r => r.WardId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(r => r.Beds)
                .WithOne(b => b.Room)
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.OwnsOne(r => r.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<Bed>(builder =>
        {
            builder.HasKey(b => b.BedId);
            builder.Property(b => b.BedNumber).HasMaxLength(32);
            builder.Property(b => b.Status).HasMaxLength(64);
            builder.Property(b => b.IsIsolation).IsRequired();
            builder.Property(b => b.OccupiedOnUtc);
            builder.HasIndex(b => new { b.RoomId, b.BedNumber }).IsUnique();
            builder.HasIndex(b => b.OccupiedByPartyId);
            builder.HasOne(b => b.Room)
                .WithMany(r => r.Beds)
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Party>()
                .WithMany()
                .HasForeignKey(b => b.OccupiedByPartyId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.OwnsOne(b => b.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<LabOrder>(builder =>
        {
            builder.HasKey(o => o.LabOrderId);
            builder.Property(o => o.OrderNumber).HasMaxLength(64);
            builder.Property(o => o.TestCode).HasMaxLength(128);
            builder.Property(o => o.Status).HasMaxLength(32);
            builder.Property(o => o.ExternalSystem).HasMaxLength(64);
            builder.Property(o => o.ExternalOrderId).HasMaxLength(64);
            builder.Property(o => o.OrderingProvider).HasMaxLength(256);
            builder.Property(o => o.Notes).HasColumnType("TEXT");
            builder.Property(o => o.OrderedOnUtc).IsRequired();
            builder.HasIndex(o => o.OrderNumber).IsUnique();
            builder.HasIndex(o => new { o.ExternalSystem, o.ExternalOrderId }).IsUnique();
            builder.HasMany(o => o.Results)
                .WithOne(r => r.LabOrder)
                .HasForeignKey(r => r.LabOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.OwnsOne(o => o.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<OrderResult>(builder =>
        {
            builder.HasKey(r => r.OrderResultId);
            builder.Property(r => r.ResultCode).HasMaxLength(64);
            builder.Property(r => r.ResultValue).HasMaxLength(256);
            builder.Property(r => r.ResultStatus).HasMaxLength(32);
            builder.Property(r => r.Units).HasMaxLength(32);
            builder.Property(r => r.ReferenceRange).HasMaxLength(128);
            builder.Property(r => r.PerformedBy).HasMaxLength(128);
            builder.Property(r => r.CollectedOnUtc).IsRequired();
            builder.HasIndex(r => new { r.LabOrderId, r.CollectedOnUtc });
            builder.OwnsOne(r => r.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });

        modelBuilder.Entity<OrderSet>(builder =>
        {
            builder.HasKey(o => o.OrderSetId);
            builder.Property(o => o.Name).HasMaxLength(128);
            builder.Property(o => o.Version).HasMaxLength(32);
            builder.Property(o => o.Description).HasMaxLength(512);
            builder.Property(o => o.ItemsJson).HasColumnType("TEXT");
            builder.HasIndex(o => new { o.Name, o.Version }).IsUnique();
            builder.OwnsOne(o => o.Audit, audit =>
            {
                audit.Property(a => a.CreatedBy).HasMaxLength(64);
                audit.Property(a => a.ModifiedBy).HasMaxLength(64);
                audit.Property(a => a.SourceModule).HasMaxLength(128);
            });
        });
    }
}
