using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class TelemetryService : ITelemetryService
{
    private const string QueryMetricKey = "Infrastructure.QueryDuration";
    private static readonly TimeSpan AggregationWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan OverviewWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan EventRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan AggregateRetention = TimeSpan.FromDays(30);

    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;

    public TelemetryService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<TelemetryAggregationOutcomeDto> AggregateAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var threshold = await EnsureQueryThresholdAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var nowUtc = DateTime.UtcNow;
        var windowStart = nowUtc - AggregationWindow;

        var durations = await dbContext.TelemetryEvents
            .AsNoTracking()
            .Where(e => e.EventType == "Query" && e.OccurredOnUtc >= windowStart)
            .Select(e => e.DurationMilliseconds)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        durations.Sort();

        var sampleCount = durations.Count;
        var average = sampleCount > 0 ? durations.Average() : 0d;
        var max = sampleCount > 0 ? durations[^1] : 0d;
        var p95 = sampleCount > 0 ? CalculatePercentile(durations, 0.95d) : 0d;
        var warningCount = sampleCount > 0 ? durations.Count(value => value >= threshold.WarningThresholdMs) : 0;
        var criticalCount = sampleCount > 0 ? durations.Count(value => value >= threshold.CriticalThresholdMs) : 0;

        var aggregate = new TelemetryAggregate
        {
            TelemetryAggregateId = _idGenerator.Create(),
            MetricKey = QueryMetricKey,
            PeriodStartUtc = windowStart,
            PeriodEndUtc = nowUtc,
            SampleCount = sampleCount,
            AverageDurationMs = Math.Round(average, 1, MidpointRounding.AwayFromZero),
            MaxDurationMs = Math.Round(max, 1, MidpointRounding.AwayFromZero),
            P95DurationMs = Math.Round(p95, 1, MidpointRounding.AwayFromZero),
            WarningCount = warningCount,
            CriticalCount = criticalCount,
            Audit = new AuditTrail
            {
                CreatedOnUtc = nowUtc,
                CreatedBy = "telemetry-aggregator",
                SourceModule = "Infrastructure"
            }
        };

        dbContext.TelemetryAggregates.Add(aggregate);

        var status = DetermineStatus(aggregate, threshold);
        var message = BuildMessage(status, aggregate, threshold);

        var health = new TelemetryHealthSnapshot
        {
            TelemetryHealthSnapshotId = _idGenerator.Create(),
            MetricKey = QueryMetricKey,
            Status = status,
            Message = message,
            CapturedOnUtc = nowUtc,
            SampleCount = sampleCount,
            WarningCount = warningCount,
            CriticalCount = criticalCount,
            P95DurationMs = aggregate.P95DurationMs,
            MaxDurationMs = aggregate.MaxDurationMs,
            Audit = new AuditTrail
            {
                CreatedOnUtc = nowUtc,
                CreatedBy = "telemetry-aggregator",
                SourceModule = "Infrastructure"
            }
        };

        dbContext.TelemetryHealthSnapshots.Add(health);

        var prunedEvents = await dbContext.TelemetryEvents
            .Where(e => e.OccurredOnUtc < nowUtc - EventRetention)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await dbContext.TelemetryAggregates
            .Where(a => a.PeriodEndUtc < nowUtc - AggregateRetention)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await dbContext.TelemetryHealthSnapshots
            .Where(h => h.CapturedOnUtc < nowUtc - AggregateRetention)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TelemetryAggregationOutcomeDto(
            Map(health),
            Map(aggregate, threshold),
            prunedEvents);
    }

    public async Task<TelemetryOverviewDto> GetOverviewAsync(int take = 24, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var threshold = await EnsureQueryThresholdAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var health = await dbContext.TelemetryHealthSnapshots
            .AsNoTracking()
            .OrderByDescending(h => h.CapturedOnUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var cutoffUtc = DateTime.UtcNow - OverviewWindow;

        var aggregateEntities = await dbContext.TelemetryAggregates
            .AsNoTracking()
            .Where(a => a.MetricKey == QueryMetricKey && a.PeriodEndUtc >= cutoffUtc)
            .OrderByDescending(a => a.PeriodEndUtc)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var aggregates = aggregateEntities
            .Select(entity => Map(entity, threshold))
            .ToList();

        var severityBreakdown = aggregates
            .GroupBy(a => a.Severity)
            .Select(group => new TelemetrySeverityBucketDto(group.Key, group.Count()))
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Severity)
            .ToList();

        var healthDto = health is null
            ? CreateDefaultHealth()
            : Map(health);

        return new TelemetryOverviewDto(healthDto, aggregates, severityBreakdown);
    }

    public async Task<TelemetryHealthSnapshotDto> GetLatestHealthAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var health = await dbContext.TelemetryHealthSnapshots
            .AsNoTracking()
            .OrderByDescending(h => h.CapturedOnUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return health is null ? CreateDefaultHealth() : Map(health);
    }

    public async Task<IReadOnlyList<TelemetrySeverityBucketDto>> GetSeverityBreakdownAsync(TimeSpan window, CancellationToken cancellationToken = default)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be positive.");
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var threshold = await EnsureQueryThresholdAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var cutoffUtc = DateTime.UtcNow - window;

        var aggregates = await dbContext.TelemetryAggregates
            .AsNoTracking()
            .Where(a => a.MetricKey == QueryMetricKey && a.PeriodEndUtc >= cutoffUtc)
            .OrderByDescending(a => a.PeriodEndUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var buckets = aggregates
            .Select(a => Map(a, threshold))
            .GroupBy(dto => dto.Severity)
            .Select(group => new TelemetrySeverityBucketDto(group.Key, group.Count()))
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Severity)
            .ToList();

        return buckets;
    }

    private static TelemetryHealthSnapshotDto CreateDefaultHealth()
    {
        var nowUtc = DateTime.UtcNow;
        return new TelemetryHealthSnapshotDto(
            "Unknown",
            "Telemetry has not produced a health snapshot yet.",
            nowUtc,
            QueryMetricKey,
            0,
            0,
            0,
            0,
            0);
    }

    private static string DetermineStatus(TelemetryAggregate aggregate, TelemetryThreshold threshold)
    {
        if (aggregate.CriticalCount > 0 || aggregate.P95DurationMs >= threshold.CriticalThresholdMs)
        {
            return "Critical";
        }

        if (aggregate.WarningCount > 0 || aggregate.P95DurationMs >= threshold.WarningThresholdMs)
        {
            return "Warning";
        }

        return "Healthy";
    }

    private static string BuildMessage(string status, TelemetryAggregate aggregate, TelemetryThreshold threshold)
    {
        return status switch
        {
            "Critical" => $"Slow-query telemetry exceeds critical threshold ({aggregate.P95DurationMs:F0}ms p95 vs ≥{threshold.CriticalThresholdMs:F0}ms).",
            "Warning" => $"Slow-query telemetry nearing limits ({aggregate.P95DurationMs:F0}ms p95 vs ≥{threshold.WarningThresholdMs:F0}ms warning).",
            _ when aggregate.SampleCount == 0 => "No slow query telemetry captured in the last hour.",
            _ => $"Slow-query telemetry nominal ({aggregate.P95DurationMs:F0}ms p95 across {aggregate.SampleCount} events)."
        };
    }

    private static double CalculatePercentile(IReadOnlyList<double> orderedValues, double percentile)
    {
        if (orderedValues.Count == 0)
        {
            return 0d;
        }

        if (orderedValues.Count == 1)
        {
            return orderedValues[0];
        }

        var position = (orderedValues.Count - 1) * percentile;
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
        {
            return orderedValues[lowerIndex];
        }

        var lowerValue = orderedValues[lowerIndex];
        var upperValue = orderedValues[upperIndex];
        var fraction = position - lowerIndex;
        return lowerValue + (upperValue - lowerValue) * fraction;
    }

    private static TelemetryHealthSnapshotDto Map(TelemetryHealthSnapshot entity)
    {
        return new TelemetryHealthSnapshotDto(
            entity.Status,
            entity.Message,
            entity.CapturedOnUtc,
            entity.MetricKey,
            entity.SampleCount,
            entity.WarningCount,
            entity.CriticalCount,
            entity.P95DurationMs,
            entity.MaxDurationMs);
    }

    private static TelemetryAggregateDto Map(TelemetryAggregate entity, TelemetryThreshold threshold)
    {
        var severity = DetermineStatus(entity, threshold);
        return new TelemetryAggregateDto(
            entity.MetricKey,
            entity.PeriodStartUtc,
            entity.PeriodEndUtc,
            entity.SampleCount,
            entity.AverageDurationMs,
            entity.P95DurationMs,
            entity.MaxDurationMs,
            entity.WarningCount,
            entity.CriticalCount,
            severity);
    }

    private async Task<TelemetryThreshold> EnsureQueryThresholdAsync(ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        var threshold = await dbContext.TelemetryThresholds
            .FirstOrDefaultAsync(t => t.MetricKey == QueryMetricKey, cancellationToken)
            .ConfigureAwait(false);

        if (threshold is not null)
        {
            return threshold;
        }

        threshold = new TelemetryThreshold
        {
            TelemetryThresholdId = _idGenerator.Create(),
            MetricKey = QueryMetricKey,
            WarningThresholdMs = 750d,
            CriticalThresholdMs = 1500d,
            Audit = new AuditTrail
            {
                CreatedOnUtc = DateTime.UtcNow,
                CreatedBy = "telemetry-aggregator",
                SourceModule = "Infrastructure"
            }
        };

        dbContext.TelemetryThresholds.Add(threshold);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return threshold;
    }
}
