using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using ExcellCore.Module.Abstractions;
using ExcellCore.Module.Extensions.Reporting.ViewModels;
using Xunit;

namespace ExcellCore.Tests;

public sealed class TelemetryWorkspaceViewModelTests
{
    [Fact]
    public async Task InitializeAsync_PopulatesCollectionsAndHealth()
    {
        var now = DateTime.UtcNow;
        var aggregates = new List<TelemetryAggregateDto>
        {
            new(
                "Infrastructure.QueryDuration",
                now.AddHours(-1),
                now,
                5,
                120,
                680,
                910,
                1,
                0,
                "Warning"),
            new(
                "Infrastructure.QueryDuration",
                now.AddHours(-2),
                now.AddHours(-1),
                12,
                140,
                480,
                600,
                0,
                0,
                "Healthy")
        };

        var health = new TelemetryHealthSnapshotDto(
            "Warning",
            "Slow-query telemetry nearing limits (680ms p95 vs ≥750ms warning).",
            now,
            "Infrastructure.QueryDuration",
            5,
            1,
            0,
            680,
            910);

        var severityBreakdown = new List<TelemetrySeverityBucketDto>
        {
            new("Warning", 3),
            new("Healthy", 5)
        };

        var overview = new TelemetryOverviewDto(health, aggregates, severityBreakdown);
        var fakeService = new FakeTelemetryService(overview, severityBreakdown);
        var viewModel = new TelemetryWorkspaceViewModel(fakeService, new StubLocalizationService(), new StubLocalizationContext());

        await viewModel.InitializeAsync();

        Assert.Equal("System Telemetry", viewModel.Title);
        Assert.Equal("Refresh", viewModel.RefreshLabel);
        Assert.Equal("Warning", viewModel.Health.Severity);
        Assert.Contains("P95", viewModel.Health.Summary);
        Assert.Equal(2, viewModel.Aggregates.Count);
        Assert.Equal("Healthy", viewModel.Aggregates[0].Severity);
        Assert.Equal("Warning", viewModel.Aggregates[^1].Severity);
        Assert.Equal(2, viewModel.SeverityBreakdown.Count);
        Assert.StartsWith("Telemetry updated", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshAsync_WhenServiceThrows_SetsErrorStatus()
    {
        var failingService = new FailingTelemetryService();
        var viewModel = new TelemetryWorkspaceViewModel(failingService, new StubLocalizationService(), new StubLocalizationContext());

        await viewModel.InitializeAsync();

        Assert.StartsWith("Failed to load telemetry", viewModel.StatusMessage);
        Assert.Empty(viewModel.Aggregates);
    }

    [Fact]
    public async Task LocalizationContextChange_UpdatesLabels()
    {
        var now = DateTime.UtcNow;
        var aggregates = new List<TelemetryAggregateDto>
        {
            new(
                "Infrastructure.QueryDuration",
                now.AddHours(-1),
                now,
                2,
                120,
                160,
                210,
                0,
                0,
                "Healthy")
        };

        var health = new TelemetryHealthSnapshotDto(
            "Healthy",
            "Nominal",
            now,
            "Infrastructure.QueryDuration",
            2,
            0,
            0,
            160,
            210);

        var severityBreakdown = new List<TelemetrySeverityBucketDto> { new("Healthy", 1) };
        var overview = new TelemetryOverviewDto(health, aggregates, severityBreakdown);

        var localized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = "Localized Telemetry",
            ["Subtitle"] = "Localized Subtitle",
            ["TrendLabel"] = "Localized Trend",
            ["SeverityBreakdownLabel"] = "Localized Severity",
            ["RefreshButton"] = "Reload",
            ["LoadingStatus"] = "Loading localized telemetry...",
            ["UpdatedStatus"] = "Updated at {0}",
            ["FailedStatus"] = "Failed localized: {0}"
        };

        var localizationService = new StubLocalizationService(localized);
        var localizationContext = new StubLocalizationContext();
        var viewModel = new TelemetryWorkspaceViewModel(new FakeTelemetryService(overview, severityBreakdown), localizationService, localizationContext);

        await viewModel.InitializeAsync();

        Assert.Equal("Localized Telemetry", viewModel.Title);
        Assert.Equal("Localized Subtitle", viewModel.Subtitle);
        Assert.Equal("Reload", viewModel.RefreshLabel);

        var updated = new Dictionary<string, string>(localized, StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = "Localized Telemetry v2",
            ["RefreshButton"] = "Go"
        };

        localizationService.SetMap(updated);
        localizationContext.SetContexts(new[] { "Extensions.Reporting.Telemetry" });

        Assert.Equal("Localized Telemetry v2", viewModel.Title);
        Assert.Equal("Go", viewModel.RefreshLabel);
    }

    private sealed class FakeTelemetryService : ITelemetryService
    {
        private readonly TelemetryOverviewDto _overview;
        private readonly IReadOnlyList<TelemetrySeverityBucketDto> _breakdown;

        public FakeTelemetryService(TelemetryOverviewDto overview, IReadOnlyList<TelemetrySeverityBucketDto> breakdown)
        {
            _overview = overview;
            _breakdown = breakdown;
        }

        public Task<TelemetryAggregationOutcomeDto> AggregateAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TelemetryOverviewDto> GetOverviewAsync(int take = 24, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_overview);
        }

        public Task<TelemetryHealthSnapshotDto> GetLatestHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_overview.Health);
        }

        public Task<IReadOnlyList<TelemetrySeverityBucketDto>> GetSeverityBreakdownAsync(TimeSpan window, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_breakdown);
        }
    }

    private sealed class FailingTelemetryService : ITelemetryService
    {
        public Task<TelemetryAggregationOutcomeDto> AggregateAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TelemetryOverviewDto> GetOverviewAsync(int take = 24, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("telemetry unavailable");
        }

        public Task<TelemetryHealthSnapshotDto> GetLatestHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TelemetryHealthSnapshotDto("Unknown", "", DateTime.UtcNow, "metric", 0, 0, 0, 0, 0));
        }

        public Task<IReadOnlyList<TelemetrySeverityBucketDto>> GetSeverityBreakdownAsync(TimeSpan window, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("telemetry unavailable");
        }
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        private Dictionary<string, string> _map;

        public StubLocalizationService()
            : this(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Title"] = "System Telemetry",
                ["Subtitle"] = "Slow-query health and performance trends.",
                ["TrendLabel"] = "Telemetry Trend",
                ["SeverityBreakdownLabel"] = "Severity Breakdown",
                ["RefreshButton"] = "Refresh",
                ["LoadingStatus"] = "Loading telemetry health...",
                ["UpdatedStatus"] = "Telemetry updated {0}",
                ["FailedStatus"] = "Failed to load telemetry: {0}"
            })
        {
        }

        public StubLocalizationService(Dictionary<string, string> map)
        {
            _map = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        }

        public string GetString(string key, IEnumerable<string> contexts)
        {
            return _map.TryGetValue(key, out var value) ? value : key;
        }

        public IReadOnlyDictionary<string, string> GetStrings(IEnumerable<string> keys, IEnumerable<string> contexts)
        {
            return keys.ToDictionary(key => key, key => GetString(key, contexts), StringComparer.OrdinalIgnoreCase);
        }

        public void SetMap(Dictionary<string, string> map)
        {
            _map = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed class StubLocalizationContext : ILocalizationContext
    {
        public IReadOnlyList<string> Contexts { get; private set; } = Array.Empty<string>();

        public event EventHandler? ContextsChanged;

        public void SetContexts(IEnumerable<string> contexts)
        {
            Contexts = (contexts ?? Array.Empty<string>()).ToList();
            ContextsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
