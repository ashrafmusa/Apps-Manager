using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class SlaReportingService : ISlaReportingService
{
    private static readonly TimeSpan EscalationThreshold = TimeSpan.FromHours(24);
    private static readonly TimeSpan ActivityWindow = TimeSpan.FromDays(7);

    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ITelemetryService _telemetryService;

    public SlaReportingService(IDbContextFactory<ExcellCoreContext> contextFactory, ITelemetryService telemetryService)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
    }

    public async Task<SlaWorkspaceSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var pendingApprovals = await dbContext.AgreementApprovals
            .AsNoTracking()
            .Where(a => a.Decision == ApprovalDecision.Pending)
            .Select(a => new
            {
                a.AgreementApprovalId,
                a.AgreementId,
                a.Approver,
                a.RequestedOnUtc,
                a.EscalatedOnUtc
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var actionWindowStart = nowUtc - ActivityWindow;

        var remindersLast7Days = await dbContext.ActionLogs
            .AsNoTracking()
            .CountAsync(log => log.ActionType == ActionLogType.Reminder && log.ActionedOnUtc >= actionWindowStart, cancellationToken)
            .ConfigureAwait(false);

        var fastTracksLast7Days = await dbContext.ActionLogs
            .AsNoTracking()
            .CountAsync(log => log.ActionType == ActionLogType.FastTrack && log.ActionedOnUtc >= actionWindowStart, cancellationToken)
            .ConfigureAwait(false);

        var telemetryHealth = await _telemetryService.GetLatestHealthAsync(cancellationToken).ConfigureAwait(false);
        var telemetryFactor = ResolveTelemetryFactor(telemetryHealth.Status);

        if (pendingApprovals.Count == 0)
        {
            var emptySummary = new SlaWorkspaceSummaryDto(0, 0, remindersLast7Days, fastTracksLast7Days);
            return new SlaWorkspaceSnapshotDto(emptySummary, Array.Empty<SlaHeatMapCellDto>(), Array.Empty<SlaEscalationDetailDto>(), Array.Empty<SlaPredictiveCardDto>());
        }

        var agreementIds = pendingApprovals
            .Select(a => a.AgreementId)
            .Distinct()
            .ToList();

        var agreements = await dbContext.Agreements
            .AsNoTracking()
            .Where(a => agreementIds.Contains(a.AgreementId))
            .Select(a => new
            {
                a.AgreementId,
                a.AgreementName,
                a.Audit.SourceModule
            })
            .ToDictionaryAsync(a => a.AgreementId, cancellationToken)
            .ConfigureAwait(false);

        var rateSummaries = await dbContext.AgreementRates
            .AsNoTracking()
            .Where(r => agreementIds.Contains(r.AgreementId))
            .GroupBy(r => r.AgreementId)
            .Select(g => new
            {
                AgreementId = g.Key,
                TotalBase = g.Sum(r => (double)r.BaseAmount)
            })
            .ToDictionaryAsync(r => r.AgreementId, cancellationToken)
            .ConfigureAwait(false);

        var impactedRecords = await dbContext.AgreementImpactedParties
            .AsNoTracking()
            .Where(ip => agreementIds.Contains(ip.AgreementId))
            .Select(ip => new
            {
                ip.AgreementId,
                ip.PartyId
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var impactedPartyIds = impactedRecords
            .Select(r => r.PartyId)
            .Distinct()
            .ToList();

        var parties = impactedPartyIds.Count == 0
            ? new Dictionary<Guid, (string Name, string Type)>()
            : await dbContext.Parties
                .AsNoTracking()
                .Where(p => impactedPartyIds.Contains(p.PartyId))
                .Select(p => new
                {
                    p.PartyId,
                    p.DisplayName,
                    p.PartyType
                })
                .ToDictionaryAsync(p => p.PartyId, p => (Name: p.DisplayName, Type: p.PartyType), cancellationToken)
                .ConfigureAwait(false);

        var actionLogs = await dbContext.ActionLogs
            .AsNoTracking()
            .Where(log => agreementIds.Contains(log.AgreementId))
            .Select(log => new ActionLogSnapshot(
                log.AgreementApprovalId,
                log.ActionType,
                log.ActionedOnUtc,
                log.Audit.SourceModule))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var actionLogsByApproval = actionLogs
            .Where(log => log.AgreementApprovalId.HasValue)
            .GroupBy(log => log.AgreementApprovalId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var impactedLookup = impactedRecords
            .GroupBy(r => r.AgreementId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Select(record => parties.TryGetValue(record.PartyId, out var detail) ? detail : (Name: string.Empty, Type: string.Empty))
                    .Where(detail => !string.IsNullOrWhiteSpace(detail.Name))
                    .ToList());

        var approvalDetails = new List<(SlaEscalationDetailDto Escalation, string Module, bool IsBreached)>();
        var predictiveCards = new List<SlaPredictiveCardDto>();

        foreach (var pending in pendingApprovals)
        {
            if (!agreements.TryGetValue(pending.AgreementId, out var agreement))
            {
                continue;
            }

            var impactedParties = impactedLookup.TryGetValue(pending.AgreementId, out var impacted)
                ? impacted
                : new List<(string Name, string Type)>();

            var potentialValue = rateSummaries.TryGetValue(pending.AgreementId, out var rateSummary)
                ? Convert.ToDecimal(rateSummary.TotalBase)
                : 0m;

            var latestAction = actionLogs
                .Where(l => l.AgreementApprovalId == pending.AgreementApprovalId)
                .OrderByDescending(l => l.ActionedOnUtc)
                .FirstOrDefault();

            var module = ResolveModule(agreement.SourceModule, latestAction?.SourceModule, impactedParties.Select(p => p.Type ?? string.Empty));
            var age = nowUtc - pending.RequestedOnUtc;
            if (age < TimeSpan.Zero)
            {
                age = TimeSpan.Zero;
            }

            var breached = pending.EscalatedOnUtc.HasValue || age >= EscalationThreshold;

            var partyNames = impactedParties.Select(p => p.Name).ToList();

            var escalationDetail = new SlaEscalationDetailDto(
                pending.AgreementId,
                pending.AgreementApprovalId,
                agreement.AgreementName,
                module,
                pending.Approver,
                pending.RequestedOnUtc,
                age,
                potentialValue,
                partyNames);

            approvalDetails.Add((escalationDetail, module, breached));

            if (breached)
            {
                continue;
            }

            var approvalLogKey = pending.AgreementApprovalId;
            var approvalLogList = approvalLogKey != Guid.Empty && actionLogsByApproval.TryGetValue(approvalLogKey, out var logsForApproval)
                ? logsForApproval
                : new List<ActionLogSnapshot>();

            var reminderCount = approvalLogList.Count(log => log.ActionType == ActionLogType.Reminder);
            var fastTrackCount = approvalLogList.Count(log => log.ActionType == ActionLogType.FastTrack);

            var normalizedAge = Math.Clamp(age.TotalHours / EscalationThreshold.TotalHours, 0d, 1d);
            var valueFactor = potentialValue >= 5000m ? 0.2d : potentialValue >= 2000m ? 0.1d : 0d;
            var historyFactor = reminderCount > 0 ? 0.05d : 0d;
            historyFactor += fastTrackCount > 0 ? 0.1d : 0d;

            var riskScore = Math.Min(1d, Math.Max(0d, normalizedAge * 0.55d + telemetryFactor + valueFactor + historyFactor));
            var riskLevel = riskScore >= 0.75d ? "High" : riskScore >= 0.45d ? "Medium" : "Low";

            if (!string.Equals(riskLevel, "Low", StringComparison.OrdinalIgnoreCase))
            {
                var estimatedTimeToBreach = EscalationThreshold - age;
                if (estimatedTimeToBreach < TimeSpan.Zero)
                {
                    estimatedTimeToBreach = TimeSpan.Zero;
                }

                var driverSummary = BuildDriverSummary(telemetryHealth, reminderCount, fastTrackCount, estimatedTimeToBreach);

                predictiveCards.Add(new SlaPredictiveCardDto(
                    pending.AgreementId,
                    pending.AgreementApprovalId,
                    agreement.AgreementName,
                    module,
                    pending.Approver,
                    pending.RequestedOnUtc,
                    age,
                    estimatedTimeToBreach,
                    potentialValue,
                    riskScore,
                    riskLevel,
                    driverSummary,
                    partyNames));
            }
        }

        var groupedHeatMap = approvalDetails
            .GroupBy(item => item.Module)
            .Select(group => new SlaHeatMapCellDto(
                group.Key,
                group.Count(),
                group.Count(item => item.IsBreached),
                group.Sum(item => item.Escalation.PotentialValue)))
            .OrderBy(cell => cell.Module, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var escalations = approvalDetails
            .Where(item => item.IsBreached)
            .Select(item => item.Escalation)
            .OrderByDescending(e => e.Age)
            .ThenBy(e => e.AgreementName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var predictive = predictiveCards
            .OrderByDescending(card => card.RiskScore)
            .ThenBy(card => card.EstimatedTimeToBreach)
            .ToList();

        var summary = new SlaWorkspaceSummaryDto(
            approvalDetails.Count,
            escalations.Count,
            remindersLast7Days,
            fastTracksLast7Days);

        return new SlaWorkspaceSnapshotDto(summary, groupedHeatMap, escalations, predictive);
    }

    private static double ResolveTelemetryFactor(string status)
    {
        return status switch
        {
            "Critical" => 0.35d,
            "Warning" => 0.15d,
            _ => 0d
        };
    }

    private static string BuildDriverSummary(TelemetryHealthSnapshotDto health, int reminderCount, int fastTrackCount, TimeSpan estimatedTimeToBreach)
    {
        var reminderText = reminderCount == 0 ? "no reminders" : FormattableString.Invariant($"{reminderCount} reminder(s)");
        var fastTrackText = fastTrackCount == 0 ? "no fast-tracks" : FormattableString.Invariant($"{fastTrackCount} fast-track(s)");
        var etaText = estimatedTimeToBreach == TimeSpan.Zero
            ? "breach imminent"
            : FormattableString.Invariant($"~{Math.Max(0, Math.Floor(estimatedTimeToBreach.TotalHours)):0}h to breach");

        return FormattableString.Invariant($"{health.Status} telemetry · {etaText} · {reminderText}, {fastTrackText}");
    }

    private static string ResolveModule(string? agreementSourceModule, string? actionSourceModule, IEnumerable<string> impactedPartyTypes)
    {
        var moduleFromAction = MapModule(actionSourceModule);
        if (!string.IsNullOrEmpty(moduleFromAction))
        {
            return moduleFromAction;
        }

        var moduleFromAgreement = MapModule(agreementSourceModule);
        if (!string.IsNullOrEmpty(moduleFromAgreement))
        {
            return moduleFromAgreement;
        }

        foreach (var partyType in impactedPartyTypes)
        {
            if (string.IsNullOrWhiteSpace(partyType))
            {
                continue;
            }

            if (partyType.Contains("clinic", StringComparison.OrdinalIgnoreCase) ||
                partyType.Contains("patient", StringComparison.OrdinalIgnoreCase) ||
                partyType.Contains("care", StringComparison.OrdinalIgnoreCase))
            {
                return "Clinical";
            }

            if (partyType.Contains("retail", StringComparison.OrdinalIgnoreCase) ||
                partyType.Contains("store", StringComparison.OrdinalIgnoreCase) ||
                partyType.Contains("customer", StringComparison.OrdinalIgnoreCase) ||
                partyType.Contains("client", StringComparison.OrdinalIgnoreCase))
            {
                return "Retail";
            }
        }

        return "Core";
    }

    private static string? MapModule(string? sourceModule)
    {
        if (string.IsNullOrWhiteSpace(sourceModule))
        {
            return null;
        }

        if (sourceModule.IndexOf("Clinical", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Clinical";
        }

        if (sourceModule.IndexOf("Retail", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Retail";
        }

        if (sourceModule.IndexOf("Corporate", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Corporate";
        }

        if (sourceModule.IndexOf("Financial", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Financial";
        }

        if (sourceModule.IndexOf("Inventory", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Inventory";
        }

        if (sourceModule.IndexOf("Identity", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Identity";
        }

        return null;
    }

    private sealed record ActionLogSnapshot(Guid? AgreementApprovalId, string ActionType, DateTime ActionedOnUtc, string? SourceModule);
}
