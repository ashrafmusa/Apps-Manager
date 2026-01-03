using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record AgreementSummaryDto(Guid AgreementId, string AgreementName, string PayerName, DateOnly EffectiveFrom, DateOnly? EffectiveTo, int RateCount, string Status, DateOnly? RenewalDate);

public sealed record AgreementRateDto(Guid? AgreementRateId, string ServiceCode, decimal BaseAmount, decimal DiscountPercent, decimal? CoPayPercent);

public sealed record AgreementApprovalDto(Guid? AgreementApprovalId, string Approver, string Decision, string? Comments, DateTime RequestedOnUtc, DateTime? DecidedOnUtc);

public sealed record AgreementImpactedPartyDto(Guid? AgreementImpactedPartyId, Guid PartyId, string PartyName, string PartyType, string Relationship);

public sealed record AgreementDetailDto(
	Guid? AgreementId,
	string AgreementName,
	string PayerName,
	string CoverageType,
	DateOnly EffectiveFrom,
	DateOnly? EffectiveTo,
	IReadOnlyList<AgreementRateDto> Rates,
	string Status,
	bool RequiresApproval,
	bool AutoRenew,
	DateOnly? RenewalDate,
	DateOnly? LastRenewedOn,
	IReadOnlyList<AgreementApprovalDto> Approvals,
	IReadOnlyList<AgreementImpactedPartyDto> ImpactedParties);

public sealed record AgreementDashboardDto(
	int TotalAgreements,
	int ActiveAgreements,
	int ExpiringSoon,
	int PendingApprovals,
	int RenewalsDueSoon,
	int TotalRates,
	decimal AverageDiscountPercent,
	decimal AverageCopayPercent);

public sealed record PricingResultDto(decimal NetAmount, decimal DiscountAmount, decimal CopayAmount);

public sealed record AgreementValidationResultDto(bool IsValid, IReadOnlyList<string> Issues);

public sealed record AgreementApprovalTriageDto(
	Guid AgreementId,
	Guid AgreementApprovalId,
	string AgreementName,
	string PayerName,
	string Approver,
	DateTime RequestedOnUtc,
	DateTime? EscalatedOnUtc,
	int ImpactedIdentities,
	decimal PotentialValue,
	IReadOnlyList<string> ImpactedPartyNames);

public sealed record AgreementApprovalHeatMapBucketDto(
	string Label,
	int PendingCount,
	decimal PotentialValue);

public sealed record ApprovalActionInsightDto(
	string ActionType,
	int CountLastSevenDays,
	int CountLastThirtyDays,
	DateTime? MostRecentActionOnUtc);

public sealed record AgreementApprovalTriageSnapshotDto(
	IReadOnlyList<AgreementApprovalTriageDto> Items,
	IReadOnlyList<AgreementApprovalHeatMapBucketDto> HeatMap,
	IReadOnlyList<ApprovalActionInsightDto> ActionInsights);
