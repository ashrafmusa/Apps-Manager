using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface IInventoryAnalyticsService
{
    Task<InventoryStockSnapshotDto> GetStockSnapshotAsync(CancellationToken cancellationToken = default);
    Task<InventoryAnomalyResultDto> AnalyzeAsync(TimeSpan? window = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryAnomalySignalDto>> GetRecentAlertsAsync(int take = 10, CancellationToken cancellationToken = default);
}
