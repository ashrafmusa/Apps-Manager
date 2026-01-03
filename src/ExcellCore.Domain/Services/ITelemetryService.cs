using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface ITelemetryService
{
    Task<TelemetryAggregationOutcomeDto> AggregateAsync(CancellationToken cancellationToken = default);
    Task<TelemetryOverviewDto> GetOverviewAsync(int take = 24, CancellationToken cancellationToken = default);
    Task<TelemetryHealthSnapshotDto> GetLatestHealthAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetrySeverityBucketDto>> GetSeverityBreakdownAsync(TimeSpan window, CancellationToken cancellationToken = default);
}
