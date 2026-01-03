using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using ExcellCore.Domain.Events;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcellCore.Tests;

public sealed class AgreementServiceTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly TestEventBus _eventBus;
    private readonly AgreementService _service;

    public AgreementServiceTests()
    {
        _factory = new TestSqliteContextFactory();
        _eventBus = new TestEventBus();
        _service = new AgreementService(_factory, new SequentialGuidGenerator(), _eventBus);
    }

    [Fact]
    public async Task CalculatePriceAsync_UsesAgreementRateDiscounts()
    {
        var agreementId = Guid.NewGuid();

        await SeedAgreementAsync(new Agreement
        {
            AgreementId = agreementId,
            AgreementName = "PRC",
            PayerName = "Payer",
            CoverageType = "General",
            Status = AgreementStatus.Active,
            RequiresApproval = false,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-10),
            EffectiveTo = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(10),
            Rates =
            {
                new AgreementRate
                {
                    AgreementRateId = Guid.NewGuid(),
                    AgreementId = agreementId,
                    ServiceCode = "SRV1",
                    BaseAmount = 120m,
                    DiscountPercent = 10m,
                    CopayPercent = 5m,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        var result = await _service.CalculatePriceAsync(agreementId, "SRV1", 150m, 3);

        Assert.Equal(306m, result.NetAmount);
        Assert.Equal(36m, result.DiscountAmount);
        Assert.Equal(18m, result.CopayAmount);
    }

    [Fact]
    public async Task ValidateWorkflowAsync_FlagsMissingPendingApprovals()
    {
        var agreementId = Guid.NewGuid();

        await SeedAgreementAsync(new Agreement
        {
            AgreementId = agreementId,
            AgreementName = "Pending",
            PayerName = "Payer",
            CoverageType = "General",
            Status = AgreementStatus.PendingApproval,
            RequiresApproval = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Rates =
            {
                new AgreementRate
                {
                    AgreementRateId = Guid.NewGuid(),
                    AgreementId = agreementId,
                    ServiceCode = "SRV1",
                    BaseAmount = 100m,
                    DiscountPercent = 0m,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        var validation = await _service.ValidateWorkflowAsync(agreementId);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.Contains("Pending Approval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EscalatePendingApprovalsAsync_PublishesEventAndMarksEscalated()
    {
        var agreementId = Guid.NewGuid();
        var pendingApprovalId = Guid.NewGuid();
        var requestedOn = DateTime.UtcNow - TimeSpan.FromHours(36);

        await SeedAgreementAsync(new Agreement
        {
            AgreementId = agreementId,
            AgreementName = "Escalate",
            PayerName = "Payer",
            CoverageType = "General",
            Status = AgreementStatus.PendingApproval,
            RequiresApproval = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-1),
            Rates =
            {
                new AgreementRate
                {
                    AgreementRateId = Guid.NewGuid(),
                    AgreementId = agreementId,
                    ServiceCode = "SRV1",
                    BaseAmount = 100m,
                    DiscountPercent = 0m,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Approvals =
            {
                new AgreementApproval
                {
                    AgreementApprovalId = pendingApprovalId,
                    AgreementId = agreementId,
                    Approver = "approver@core",
                    Decision = ApprovalDecision.Pending,
                    RequestedOnUtc = requestedOn,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        var escalatedCount = await _service.EscalatePendingApprovalsAsync();

        Assert.Equal(1, escalatedCount);
        Assert.Single(_eventBus.PublishedEvents.OfType<ApprovalEscalatedEvent>());
        var escalationEvent = _eventBus.PublishedEvents.OfType<ApprovalEscalatedEvent>().Single();
        Assert.Equal(agreementId, escalationEvent.AgreementId);
        Assert.Equal("approver@core", escalationEvent.Approver);
        Assert.Equal(requestedOn, escalationEvent.RequestedOnUtc);

        await using var verificationContext = await _factory.CreateDbContextAsync();
        var escalatedApproval = await verificationContext.AgreementApprovals.SingleAsync(a => a.AgreementApprovalId == pendingApprovalId);
        Assert.NotNull(escalatedApproval.EscalatedOnUtc);
    }

    [Fact]
    public async Task GetApprovalTriageAsync_OrdersByPotentialValue()
    {
        var highValueAgreementId = Guid.NewGuid();
        var lowValueAgreementId = Guid.NewGuid();
        var lowPartyId = Guid.NewGuid();
        var highPartyOneId = Guid.NewGuid();
        var highPartyTwoId = Guid.NewGuid();

        await SeedPartyAsync(new Party
        {
            PartyId = lowPartyId,
            PartyType = "Client",
            DisplayName = "Low Identity",
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        await SeedPartyAsync(new Party
        {
            PartyId = highPartyOneId,
            PartyType = "Client",
            DisplayName = "High Identity A",
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        await SeedPartyAsync(new Party
        {
            PartyId = highPartyTwoId,
            PartyType = "Client",
            DisplayName = "High Identity B",
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        await SeedAgreementAsync(new Agreement
        {
            AgreementId = lowValueAgreementId,
            AgreementName = "Low",
            PayerName = "Payer Low",
            CoverageType = "General",
            Status = AgreementStatus.PendingApproval,
            RequiresApproval = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Rates =
            {
                new AgreementRate
                {
                    AgreementRateId = Guid.NewGuid(),
                    AgreementId = lowValueAgreementId,
                    ServiceCode = "SRV-L",
                    BaseAmount = 100m,
                    DiscountPercent = 0m,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Approvals =
            {
                new AgreementApproval
                {
                    AgreementApprovalId = Guid.NewGuid(),
                    AgreementId = lowValueAgreementId,
                    Approver = "low@core",
                    Decision = ApprovalDecision.Pending,
                    RequestedOnUtc = DateTime.UtcNow - TimeSpan.FromHours(2),
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            ImpactedParties =
            {
                new AgreementImpactedParty
                {
                    AgreementImpactedPartyId = Guid.NewGuid(),
                    AgreementId = lowValueAgreementId,
                    PartyId = lowPartyId,
                    Relationship = "Client",
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        await SeedAgreementAsync(new Agreement
        {
            AgreementId = highValueAgreementId,
            AgreementName = "High",
            PayerName = "Payer High",
            CoverageType = "General",
            Status = AgreementStatus.PendingApproval,
            RequiresApproval = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Rates =
            {
                new AgreementRate
                {
                    AgreementRateId = Guid.NewGuid(),
                    AgreementId = highValueAgreementId,
                    ServiceCode = "SRV-H1",
                    BaseAmount = 3000m,
                    DiscountPercent = 0m,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                },
                new AgreementRate
                {
                    AgreementRateId = Guid.NewGuid(),
                    AgreementId = highValueAgreementId,
                    ServiceCode = "SRV-H2",
                    BaseAmount = 2200m,
                    DiscountPercent = 0m,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Approvals =
            {
                new AgreementApproval
                {
                    AgreementApprovalId = Guid.NewGuid(),
                    AgreementId = highValueAgreementId,
                    Approver = "high@core",
                    Decision = ApprovalDecision.Pending,
                    RequestedOnUtc = DateTime.UtcNow - TimeSpan.FromHours(1),
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            ImpactedParties =
            {
                new AgreementImpactedParty
                {
                    AgreementImpactedPartyId = Guid.NewGuid(),
                    AgreementId = highValueAgreementId,
                    PartyId = highPartyOneId,
                    Relationship = "Client",
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                },
                new AgreementImpactedParty
                {
                    AgreementImpactedPartyId = Guid.NewGuid(),
                    AgreementId = highValueAgreementId,
                    PartyId = highPartyTwoId,
                    Relationship = "Client",
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        var snapshot = await _service.GetApprovalTriageAsync();
        var triage = snapshot.Items;

        Assert.Equal(2, triage.Count);
        Assert.Equal(highValueAgreementId, triage[0].AgreementId);
        Assert.Equal(2, triage[0].ImpactedIdentities);
        Assert.Equal(5200m, triage[0].PotentialValue);
        Assert.Contains("High Identity A", triage[0].ImpactedPartyNames);
        Assert.Contains("High Identity B", triage[0].ImpactedPartyNames);
        Assert.Single(triage[1].ImpactedPartyNames);

        Assert.Equal(5, snapshot.HeatMap.Count);
        var immediateBucket = Assert.Single(snapshot.HeatMap.Where(bucket => bucket.Label == "<8h"));
        Assert.Equal(2, immediateBucket.PendingCount);
        Assert.Equal(5300m, immediateBucket.PotentialValue);

        Assert.Equal(2, snapshot.ActionInsights.Count);
        foreach (var insight in snapshot.ActionInsights)
        {
            Assert.Equal(0, insight.CountLastSevenDays);
            Assert.Equal(0, insight.CountLastThirtyDays);
            Assert.Null(insight.MostRecentActionOnUtc);
        }
    }

    [Fact]
    public async Task GetApprovalTriageAsync_IncludesActionInsightsFromLogs()
    {
        var agreementId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();

        await SeedAgreementAsync(new Agreement
        {
            AgreementId = agreementId,
            AgreementName = "Insight",
            PayerName = "Insight Payer",
            CoverageType = "General",
            Status = AgreementStatus.PendingApproval,
            RequiresApproval = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Rates =
            {
                new AgreementRate
                {
                    AgreementRateId = Guid.NewGuid(),
                    AgreementId = agreementId,
                    ServiceCode = "SRV",
                    BaseAmount = 500m,
                    DiscountPercent = 0m,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Approvals =
            {
                new AgreementApproval
                {
                    AgreementApprovalId = approvalId,
                    AgreementId = agreementId,
                    Approver = "insight@core",
                    Decision = ApprovalDecision.Pending,
                    RequestedOnUtc = DateTime.UtcNow - TimeSpan.FromHours(6),
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        await _service.SendApprovalReminderAsync(agreementId, approvalId);
        await _service.FastTrackApprovalAsync(agreementId, approvalId);

        var snapshot = await _service.GetApprovalTriageAsync();

        var reminderInsight = Assert.Single(snapshot.ActionInsights.Where(i => i.ActionType == ActionLogType.Reminder));
        Assert.Equal(1, reminderInsight.CountLastSevenDays);
        Assert.Equal(1, reminderInsight.CountLastThirtyDays);
        Assert.NotNull(reminderInsight.MostRecentActionOnUtc);

        var fastTrackInsight = Assert.Single(snapshot.ActionInsights.Where(i => i.ActionType == ActionLogType.FastTrack));
        Assert.Equal(1, fastTrackInsight.CountLastSevenDays);
        Assert.Equal(1, fastTrackInsight.CountLastThirtyDays);
        Assert.NotNull(fastTrackInsight.MostRecentActionOnUtc);
    }

    [Fact]
    public async Task SendApprovalReminderAsync_PublishesReminderEvent()
    {
        var agreementId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var requestedOn = DateTime.UtcNow - TimeSpan.FromHours(3);

        await SeedAgreementAsync(new Agreement
        {
            AgreementId = agreementId,
            AgreementName = "Reminder",
            PayerName = "Payer",
            CoverageType = "General",
            Status = AgreementStatus.PendingApproval,
            RequiresApproval = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Rates =
            {
                new AgreementRate
                {
                    AgreementRateId = Guid.NewGuid(),
                    AgreementId = agreementId,
                    ServiceCode = "SRV1",
                    BaseAmount = 100m,
                    DiscountPercent = 0m,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Approvals =
            {
                new AgreementApproval
                {
                    AgreementApprovalId = approvalId,
                    AgreementId = agreementId,
                    Approver = "approver@core",
                    Decision = ApprovalDecision.Pending,
                    RequestedOnUtc = requestedOn,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        await _service.SendApprovalReminderAsync(agreementId, approvalId);

        var reminderEvent = _eventBus.PublishedEvents.OfType<ApprovalReminderEvent>().Single();
        Assert.Equal(agreementId, reminderEvent.AgreementId);
        Assert.Equal(approvalId, reminderEvent.AgreementApprovalId);
        Assert.Equal("approver@core", reminderEvent.Approver);
        Assert.Equal(requestedOn, reminderEvent.RequestedOnUtc);

        await using var verificationContext = await _factory.CreateDbContextAsync();
        var approval = await verificationContext.AgreementApprovals.SingleAsync(a => a.AgreementApprovalId == approvalId);
        Assert.NotNull(approval.Audit.ModifiedOnUtc);
        var reminderLog = await verificationContext.ActionLogs.SingleAsync(l => l.AgreementApprovalId == approvalId && l.ActionType == ActionLogType.Reminder);
        Assert.Equal("desktop", reminderLog.PerformedBy);
    }

    [Fact]
    public async Task FastTrackApprovalAsync_MarksEscalatedAndPublishesEvent()
    {
        var agreementId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var requestedOn = DateTime.UtcNow - TimeSpan.FromHours(2);

        await SeedAgreementAsync(new Agreement
        {
            AgreementId = agreementId,
            AgreementName = "Fast",
            PayerName = "Payer",
            CoverageType = "General",
            Status = AgreementStatus.PendingApproval,
            RequiresApproval = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Rates =
            {
                new AgreementRate
                {
                    AgreementRateId = Guid.NewGuid(),
                    AgreementId = agreementId,
                    ServiceCode = "SRV1",
                    BaseAmount = 350m,
                    DiscountPercent = 0m,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Approvals =
            {
                new AgreementApproval
                {
                    AgreementApprovalId = approvalId,
                    AgreementId = agreementId,
                    Approver = "approver@core",
                    Decision = ApprovalDecision.Pending,
                    RequestedOnUtc = requestedOn,
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
                }
            },
            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "tests" }
        });

        await _service.FastTrackApprovalAsync(agreementId, approvalId);

        var escalationEvent = _eventBus.PublishedEvents.OfType<ApprovalEscalatedEvent>().Single();
        Assert.Equal(agreementId, escalationEvent.AgreementId);
        Assert.Equal("approver@core", escalationEvent.Approver);

        await using var verificationContext = await _factory.CreateDbContextAsync();
        var approval = await verificationContext.AgreementApprovals.SingleAsync(a => a.AgreementApprovalId == approvalId);
        Assert.NotNull(approval.EscalatedOnUtc);
        var fastTrackLog = await verificationContext.ActionLogs.SingleAsync(l => l.AgreementApprovalId == approvalId && l.ActionType == ActionLogType.FastTrack);
        Assert.Equal(ActionLogType.FastTrack, fastTrackLog.ActionType);
    }

    private async Task SeedAgreementAsync(Agreement agreement)
    {
        await using var context = await _factory.CreateDbContextAsync();
        await context.EnsureDatabaseMigratedAsync(CancellationToken.None);
        context.Agreements.Add(agreement);
        await context.SaveChangesAsync();
    }

    private async Task SeedPartyAsync(Party party)
    {
        await using var context = await _factory.CreateDbContextAsync();
        await context.EnsureDatabaseMigratedAsync(CancellationToken.None);
        context.Parties.Add(party);
        await context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private sealed class TestEventBus : IAppEventBus
    {
        public List<object> PublishedEvents { get; } = new();

        public Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default) where TEvent : class
        {
            PublishedEvents.Add(appEvent);
            return Task.CompletedTask;
        }
    }
}
