using System;
using System.Linq;
using System.Threading.Tasks;
using ExcellCore.Domain.Entities;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcellCore.Tests;

public sealed class TelemetryServiceTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly TelemetryService _service;

    public TelemetryServiceTests()
    {
        _factory = new TestSqliteContextFactory();
        _service = new TelemetryService(_factory, new SequentialGuidGenerator());
    }

    [Fact]
    public async Task AggregateAsync_SeedsThresholdsAndReturnsHealthyWhenNoEvents()
    {
        var outcome = await _service.AggregateAsync();

        Assert.NotNull(outcome);
        Assert.Equal("Healthy", outcome.Health.Status);
        Assert.Equal("Infrastructure.QueryDuration", outcome.Health.MetricKey);
        Assert.Equal(0, outcome.Aggregate.SampleCount);
        Assert.Equal("Healthy", outcome.Aggregate.Severity);

        await using var context = await _factory.CreateDbContextAsync();
        var thresholdCount = await context.TelemetryThresholds.CountAsync();
        Assert.Equal(1, thresholdCount);
    }

    [Fact]
    public async Task AggregateAsync_DetectsCriticalWhenThresholdExceeded()
    {
        await using (var seedContext = await _factory.CreateDbContextAsync())
        {
            await seedContext.TelemetryThresholds.AddAsync(new TelemetryThreshold
            {
                MetricKey = "Infrastructure.QueryDuration",
                WarningThresholdMs = 750,
                CriticalThresholdMs = 1500,
                Audit = new AuditTrail
                {
                    CreatedOnUtc = DateTime.UtcNow.AddMinutes(-30),
                    CreatedBy = "tests",
                    SourceModule = "tests"
                }
            });

            await seedContext.TelemetryEvents.AddRangeAsync(
                new TelemetryEvent
                {
                    EventType = "Query",
                    DurationMilliseconds = 600,
                    OccurredOnUtc = DateTime.UtcNow.AddMinutes(-20),
                    Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "tests" }
                },
                new TelemetryEvent
                {
                    EventType = "Query",
                    DurationMilliseconds = 1800,
                    OccurredOnUtc = DateTime.UtcNow.AddMinutes(-5),
                    Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "tests" }
                });

            await seedContext.SaveChangesAsync();
        }

        var outcome = await _service.AggregateAsync();

        Assert.Equal("Critical", outcome.Health.Status);
        Assert.True(outcome.Aggregate.CriticalCount >= 1);
        Assert.Contains("critical", outcome.Health.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Critical", outcome.Aggregate.Severity);
    }

    [Fact]
    public async Task GetOverviewAsync_ReturnsAggregatesWithSeverityBreakdown()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            await context.TelemetryEvents.AddAsync(new TelemetryEvent
            {
                EventType = "Query",
                DurationMilliseconds = 200,
                OccurredOnUtc = DateTime.UtcNow.AddMinutes(-5),
                Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "tests" }
            });

            await context.SaveChangesAsync();
        }

        await _service.AggregateAsync();

        var overview = await _service.GetOverviewAsync();

        Assert.NotNull(overview.Health);
        Assert.NotEmpty(overview.Aggregates);
        Assert.All(overview.Aggregates, aggregate => Assert.False(string.IsNullOrWhiteSpace(aggregate.Severity)));
        Assert.NotNull(overview.SeverityBreakdown);
    }

    [Fact]
    public async Task GetSeverityBreakdownAsync_GroupsAggregatesBySeverity()
    {
        await using (var context = await _factory.CreateDbContextAsync())
        {
            await context.TelemetryEvents.AddAsync(new TelemetryEvent
            {
                EventType = "Query",
                DurationMilliseconds = 1900,
                OccurredOnUtc = DateTime.UtcNow.AddMinutes(-10),
                Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "tests" }
            });

            await context.SaveChangesAsync();
        }

        await _service.AggregateAsync();

        await using (var resetContext = await _factory.CreateDbContextAsync())
        {
            await resetContext.TelemetryEvents.ExecuteDeleteAsync();
            await resetContext.SaveChangesAsync();

            await resetContext.TelemetryEvents.AddAsync(new TelemetryEvent
            {
                EventType = "Query",
                DurationMilliseconds = 200,
                OccurredOnUtc = DateTime.UtcNow.AddMinutes(-5),
                Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "tests" }
            });

            await resetContext.SaveChangesAsync();
        }

        await _service.AggregateAsync();

        var breakdown = await _service.GetSeverityBreakdownAsync(TimeSpan.FromHours(24));
        var severityMap = breakdown.ToDictionary(bucket => bucket.Severity, bucket => bucket.Count);

        Assert.True(severityMap.TryGetValue("Critical", out var criticalCount) && criticalCount >= 1);
        Assert.True(severityMap.TryGetValue("Healthy", out var healthyCount) && healthyCount >= 1);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
