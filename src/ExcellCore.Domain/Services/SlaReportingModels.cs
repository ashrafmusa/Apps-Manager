using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record SlaWorkspaceSummaryDto(int PendingApprovals, int BreachedApprovals, int RemindersLast7Days, int FastTracksLast7Days);

public sealed record SlaHeatMapCellDto(string Module, int PendingCount, int BreachCount, decimal PotentialValue);

public sealed record SlaEscalationDetailDto(
    Guid AgreementId,
    Guid ApprovalId,
    string AgreementName,
    string Module,
    string Approver,
    DateTime RequestedOnUtc,
    TimeSpan Age,
    decimal PotentialValue,
    IReadOnlyList<string> ImpactedParties);

public sealed record SlaWorkspaceSnapshotDto(
    SlaWorkspaceSummaryDto Summary,
    IReadOnlyList<SlaHeatMapCellDto> HeatMap,
    IReadOnlyList<SlaEscalationDetailDto> Escalations,
    IReadOnlyList<SlaPredictiveCardDto> PredictiveCards);

public sealed record SlaPredictiveCardDto(
    Guid AgreementId,
    Guid ApprovalId,
    string AgreementName,
    string Module,
    string Approver,
    DateTime RequestedOnUtc,
    TimeSpan Age,
    TimeSpan EstimatedTimeToBreach,
    decimal PotentialValue,
    double RiskScore,
    string RiskLevel,
    string DriverSummary,
    IReadOnlyList<string> ImpactedParties);
