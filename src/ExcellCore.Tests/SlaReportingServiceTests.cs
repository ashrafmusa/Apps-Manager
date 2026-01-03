using System;
using System.Linq;
using System.Threading.Tasks;
using ExcellCore.Domain.Entities;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using Xunit;

namespace ExcellCore.Tests;

public sealed class SlaReportingServiceTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly TelemetryService _telemetryService;
    private readonly SlaReportingService _service;

    public SlaReportingServiceTests()
    {
        _factory = new TestSqliteContextFactory();
        _telemetryService = new TelemetryService(_factory, new SequentialGuidGenerator());
        _service = new SlaReportingService(_factory, _telemetryService);
    }

    [Fact]
    public async Task GetSnapshotAsync_NoPendingApprovals_ReturnsEmptySnapshot()
    {
        var snapshot = await _service.GetSnapshotAsync();

        Assert.Equal(0, snapshot.Summary.PendingApprovals);
        Assert.Empty(snapshot.HeatMap);
        Assert.Empty(snapshot.Escalations);
    }

    [Fact]
    public async Task GetSnapshotAsync_GroupsBreachesByModule()
    {
        var nowUtc = DateTime.UtcNow;
        var clinicalAgreementId = Guid.NewGuid();
        var retailAgreementId = Guid.NewGuid();
        var clinicalApprovalId = Guid.NewGuid();
        var retailApprovalId = Guid.NewGuid();
        var clinicalPartyId = Guid.NewGuid();
        var retailPartyId = Guid.NewGuid();

        await using (var context = await _factory.CreateDbContextAsync())
        {
            await context.Agreements.AddRangeAsync(
                new Agreement
                {
                    AgreementId = clinicalAgreementId,
                    AgreementName = "Clinical SLA",
                    CoverageType = "Clinical",
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Clinical" },
                    Rates =
                    {
                        new AgreementRate
                        {
                            AgreementRateId = Guid.NewGuid(),
                            AgreementId = clinicalAgreementId,
                            ServiceCode = "CLN",
                            BaseAmount = 5000m,
                            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Clinical" }
                        }
                    },
                    Approvals =
                    {
                        new AgreementApproval
                        {
                            AgreementApprovalId = clinicalApprovalId,
                            AgreementId = clinicalAgreementId,
                            Approver = "clinical.approver@core",
                            Decision = ApprovalDecision.Pending,
                            RequestedOnUtc = nowUtc - TimeSpan.FromHours(30),
                            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Clinical" }
                        }
                    },
                    ImpactedParties =
                    {
                        new AgreementImpactedParty
                        {
                            AgreementImpactedPartyId = Guid.NewGuid(),
                            AgreementId = clinicalAgreementId,
                            PartyId = clinicalPartyId,
                            Relationship = "Clinic",
                            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Clinical" }
                        }
                    }
                },
                new Agreement
                {
                    AgreementId = retailAgreementId,
                    AgreementName = "Retail SLA",
                    CoverageType = "Retail",
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Retail" },
                    Rates =
                    {
                        new AgreementRate
                        {
                            AgreementRateId = Guid.NewGuid(),
                            AgreementId = retailAgreementId,
                            ServiceCode = "RTL",
                            BaseAmount = 1500m,
                            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Retail" }
                        }
                    },
                    Approvals =
                    {
                        new AgreementApproval
                        {
                            AgreementApprovalId = retailApprovalId,
                            AgreementId = retailAgreementId,
                            Approver = "retail.approver@core",
                            Decision = ApprovalDecision.Pending,
                            RequestedOnUtc = nowUtc - TimeSpan.FromHours(12),
                            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Retail" }
                        }
                    },
                    ImpactedParties =
                    {
                        new AgreementImpactedParty
                        {
                            AgreementImpactedPartyId = Guid.NewGuid(),
                            AgreementId = retailAgreementId,
                            PartyId = retailPartyId,
                            Relationship = "Store",
                            Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Retail" }
                        }
                    }
                });

            await context.Parties.AddRangeAsync(
                new Party
                {
                    PartyId = clinicalPartyId,
                    DisplayName = "Clinic North",
                    PartyType = "Clinic",
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Clinical" }
                },
                new Party
                {
                    PartyId = retailPartyId,
                    DisplayName = "Retail West",
                    PartyType = "Retail",
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Retail" }
                });

            await context.ActionLogs.AddRangeAsync(
                new ActionLog
                {
                    ActionLogId = Guid.NewGuid(),
                    AgreementId = clinicalAgreementId,
                    AgreementApprovalId = clinicalApprovalId,
                    ActionType = ActionLogType.Reminder,
                    PerformedBy = "system",
                    ActionedOnUtc = nowUtc - TimeSpan.FromDays(1),
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Clinical" }
                },
                new ActionLog
                {
                    ActionLogId = Guid.NewGuid(),
                    AgreementId = retailAgreementId,
                    AgreementApprovalId = retailApprovalId,
                    ActionType = ActionLogType.FastTrack,
                    PerformedBy = "system",
                    ActionedOnUtc = nowUtc - TimeSpan.FromHours(6),
                    Audit = new AuditTrail { CreatedBy = "test", SourceModule = "IS.Retail" }
                });

            await context.SaveChangesAsync();
        }

        var snapshot = await _service.GetSnapshotAsync();

        Assert.Equal(2, snapshot.Summary.PendingApprovals);
        Assert.Equal(1, snapshot.Summary.BreachedApprovals);
        Assert.Equal(1, snapshot.Summary.RemindersLast7Days);
        Assert.Equal(1, snapshot.Summary.FastTracksLast7Days);

        var clinicalCell = Assert.Single(snapshot.HeatMap.Where(cell => cell.Module == "Clinical"));
        Assert.Equal(1, clinicalCell.BreachCount);
        Assert.Equal(1, clinicalCell.PendingCount);

        var retailCell = Assert.Single(snapshot.HeatMap.Where(cell => cell.Module == "Retail"));
        Assert.Equal(0, retailCell.BreachCount);
        Assert.Equal(1, retailCell.PendingCount);

        var escalation = Assert.Single(snapshot.Escalations);
        Assert.Equal(clinicalAgreementId, escalation.AgreementId);
        Assert.Equal(clinicalApprovalId, escalation.ApprovalId);
        Assert.Equal("Clinical", escalation.Module);
        Assert.Contains("Clinic North", escalation.ImpactedParties);
        Assert.True(escalation.Age >= TimeSpan.FromHours(24));
    }

    [Fact]
    public async Task GetSnapshotAsync_ProducesPredictiveCardsWhenRiskIsHigh()
    {
        var nowUtc = DateTime.UtcNow;
        var agreementId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using (var context = await _factory.CreateDbContextAsync())
        {
            await context.TelemetryEvents.AddAsync(new TelemetryEvent
            {
                EventType = "Query",
                DurationMilliseconds = 2400,
                OccurredOnUtc = nowUtc.AddMinutes(-10),
                Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "tests" }
            });

            await context.Agreements.AddAsync(new Agreement
            {
                AgreementId = agreementId,
                AgreementName = "Predictive SLA",
                CoverageType = "Clinical",
                Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "IS.Clinical" },
                Rates =
                {
                    new AgreementRate
                    {
                        AgreementRateId = Guid.NewGuid(),
                        AgreementId = agreementId,
                        ServiceCode = "CLN",
                        BaseAmount = 6000m,
                        Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "IS.Clinical" }
                    }
                },
                Approvals =
                {
                    new AgreementApproval
                    {
                        AgreementApprovalId = approvalId,
                        AgreementId = agreementId,
                        Approver = "clinical.approver@core",
                        Decision = ApprovalDecision.Pending,
                        RequestedOnUtc = nowUtc - TimeSpan.FromHours(18),
                        Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "IS.Clinical" }
                    }
                },
                ImpactedParties =
                {
                    new AgreementImpactedParty
                    {
                        AgreementImpactedPartyId = Guid.NewGuid(),
                        AgreementId = agreementId,
                        PartyId = partyId,
                        Relationship = "Clinic",
                        Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "IS.Clinical" }
                    }
                }
            });

            await context.Parties.AddAsync(new Party
            {
                PartyId = partyId,
                DisplayName = "Clinic North",
                PartyType = "Clinic",
                Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "IS.Clinical" }
            });

            await context.SaveChangesAsync();
        }

        await _telemetryService.AggregateAsync();

        var snapshot = await _service.GetSnapshotAsync();

        Assert.NotEmpty(snapshot.PredictiveCards);
        var card = snapshot.PredictiveCards.First();
        Assert.Equal(approvalId, card.ApprovalId);
        Assert.True(card.RiskScore > 0.45d);
        Assert.False(string.IsNullOrWhiteSpace(card.DriverSummary));
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
