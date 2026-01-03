using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record TelemetryHealthSnapshotDto(
    string Status,
    string Message,
    DateTime CapturedOnUtc,
    string MetricKey,
    int SampleCount,
    int WarningCount,
    int CriticalCount,
    double P95DurationMs,
    double MaxDurationMs);

public sealed record TelemetryAggregateDto(
    string MetricKey,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    int SampleCount,
    double AverageDurationMs,
    double P95DurationMs,
    double MaxDurationMs,
    int WarningCount,
    int CriticalCount,
    string Severity);

public sealed record TelemetryOverviewDto(
    TelemetryHealthSnapshotDto Health,
    IReadOnlyList<TelemetryAggregateDto> Aggregates,
    IReadOnlyList<TelemetrySeverityBucketDto> SeverityBreakdown);

public sealed record TelemetryAggregationOutcomeDto(
    TelemetryHealthSnapshotDto Health,
    TelemetryAggregateDto Aggregate,
    int PrunedEventCount);

public sealed record TelemetrySeverityBucketDto(string Severity, int Count);
