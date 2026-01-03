using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class InventoryAnalyticsService : IInventoryAnalyticsService
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(48);
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;
    private readonly bool _enableSeeding;

    public InventoryAnalyticsService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator, bool enableSeeding = true)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _enableSeeding = enableSeeding;
    }

    public async Task<InventoryStockSnapshotDto> GetStockSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);
        if (_enableSeeding)
        {
            await EnsureSeedAsync(dbContext, cancellationToken).ConfigureAwait(false);
        }

        var latest = (await dbContext.InventoryLedgerEntries
                .AsNoTracking()
                .OrderByDescending(entry => entry.OccurredOnUtc)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(entry => new { entry.Sku, entry.Location })
            .Select(group => group.First())
            .OrderBy(entry => entry.Location)
            .ThenBy(entry => entry.Sku)
            .ToList();

        var items = latest
            .Select(entry => new InventoryStockItemDto(
                entry.Sku,
                entry.ItemName,
                entry.Location,
                entry.QuantityOnHand,
                entry.ReorderPoint,
                entry.OnOrder,
                entry.OccurredOnUtc))
            .ToList();

        return new InventoryStockSnapshotDto(items);
    }

    public async Task<InventoryAnomalyResultDto> AnalyzeAsync(TimeSpan? window = null, CancellationToken cancellationToken = default)
    {
        var analysisWindow = window ?? DefaultWindow;
        var windowStartUtc = DateTime.UtcNow - analysisWindow;
        var windowEndUtc = DateTime.UtcNow;

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);
        if (_enableSeeding)
        {
            await EnsureSeedAsync(dbContext, cancellationToken).ConfigureAwait(false);
        }

        var ledgerWindow = await dbContext.InventoryLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.OccurredOnUtc >= windowStartUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var signals = new List<InventoryAnomalySignalDto>();

        foreach (var group in ledgerWindow.GroupBy(entry => new { entry.Sku, entry.Location }))
        {
            var ordered = group.OrderBy(entry => entry.OccurredOnUtc).ToList();
            var first = ordered.First();
            var latest = ordered.Last();
            var durationDays = Math.Max(analysisWindow.TotalDays, 0.1d);
            var netDelta = ordered.Sum(entry => entry.QuantityDelta);
            var velocityPerDay = (double)((latest.QuantityOnHand - first.QuantityOnHand) / (decimal)durationDays);

            EvaluateStockOut(latest, windowStartUtc, windowEndUtc, velocityPerDay, signals, dbContext);
            EvaluateShrinkage(latest, first, velocityPerDay, netDelta, windowStartUtc, windowEndUtc, signals, dbContext);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new InventoryAnomalyResultDto(signals, windowStartUtc, windowEndUtc);
    }

    public async Task<IReadOnlyList<InventoryAnomalySignalDto>> GetRecentAlertsAsync(int take = 10, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var alerts = await dbContext.InventoryAnomalyAlerts
            .AsNoTracking()
            .OrderByDescending(alert => alert.DetectedOnUtc)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return alerts
            .Select(alert => new InventoryAnomalySignalDto(
                alert.Sku,
                alert.ItemName,
                alert.Location,
                alert.SignalType,
                alert.Severity,
                alert.VelocityPerDay,
                alert.QuantityOnHand,
                alert.ReorderPoint,
                alert.Message,
                alert.DetectedOnUtc))
            .ToList();
    }

    private void EvaluateStockOut(InventoryLedgerEntry latest, DateTime windowStartUtc, DateTime windowEndUtc, double velocityPerDay, List<InventoryAnomalySignalDto> signals, ExcellCoreContext dbContext)
    {
        var severity = string.Empty;
        if (latest.QuantityOnHand <= 0m)
        {
            severity = "Critical";
        }
        else if (latest.QuantityOnHand <= Math.Max(1m, latest.ReorderPoint * 0.25m))
        {
            severity = "Warning";
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            return;
        }

        var message = severity == "Critical"
            ? $"{latest.ItemName} is stocked out at {latest.Location}."
            : $"{latest.ItemName} is approaching stock-out at {latest.Location}.";

        AddAlert(latest, "StockOutRisk", severity, velocityPerDay, message, windowStartUtc, windowEndUtc, signals, dbContext);
    }

    private void EvaluateShrinkage(InventoryLedgerEntry latest, InventoryLedgerEntry first, double velocityPerDay, decimal netDelta, DateTime windowStartUtc, DateTime windowEndUtc, List<InventoryAnomalySignalDto> signals, ExcellCoreContext dbContext)
    {
        var shrinkageRate = (double)(first.QuantityOnHand == 0 ? 0m : (first.QuantityOnHand - latest.QuantityOnHand) / first.QuantityOnHand);
        var severeVelocity = velocityPerDay <= -50d || shrinkageRate >= 0.6d;
        var warningVelocity = velocityPerDay <= -20d || shrinkageRate >= 0.35d || netDelta < -25m;

        if (!severeVelocity && !warningVelocity)
        {
            return;
        }

        var severity = severeVelocity ? "Critical" : "Warning";
        var message = severity == "Critical"
            ? $"Rapid shrinkage detected for {latest.ItemName} at {latest.Location}."
            : $"Negative velocity detected for {latest.ItemName} at {latest.Location}.";

        AddAlert(latest, "Shrinkage", severity, velocityPerDay, message, windowStartUtc, windowEndUtc, signals, dbContext);
    }

    private void AddAlert(InventoryLedgerEntry latest, string signalType, string severity, double velocityPerDay, string message, DateTime windowStartUtc, DateTime windowEndUtc, List<InventoryAnomalySignalDto> signals, ExcellCoreContext dbContext)
    {
        var alert = new InventoryAnomalySignalDto(
            latest.Sku,
            latest.ItemName,
            latest.Location,
            signalType,
            severity,
            velocityPerDay,
            latest.QuantityOnHand,
            latest.ReorderPoint,
            message,
            DateTime.UtcNow);

        signals.Add(alert);

        var recentDuplicate = dbContext.InventoryAnomalyAlerts
            .Any(existing => existing.Sku == latest.Sku && existing.Location == latest.Location && existing.SignalType == signalType && existing.DetectedOnUtc >= windowStartUtc);

        if (recentDuplicate)
        {
            return;
        }

        dbContext.InventoryAnomalyAlerts.Add(new InventoryAnomalyAlert
        {
            InventoryAnomalyAlertId = _idGenerator.Create(),
            Sku = latest.Sku,
            ItemName = latest.ItemName,
            Location = latest.Location,
            SignalType = signalType,
            Severity = severity,
            Message = message,
            VelocityPerDay = velocityPerDay,
            QuantityOnHand = latest.QuantityOnHand,
            ReorderPoint = latest.ReorderPoint,
            DetectedOnUtc = DateTime.UtcNow,
            WindowStartUtc = windowStartUtc,
            WindowEndUtc = windowEndUtc,
            Audit = new AuditTrail
            {
                CreatedOnUtc = DateTime.UtcNow,
                CreatedBy = "inventory-anomaly",
                SourceModule = "Core.Inventory"
            }
        });
    }

    private async Task EnsureSeedAsync(ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.InventoryLedgerEntries.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        foreach (var seed in SeedData)
        {
            dbContext.InventoryLedgerEntries.Add(new InventoryLedgerEntry
            {
                InventoryLedgerEntryId = _idGenerator.Create(),
                Sku = seed.Sku,
                ItemName = seed.ItemName,
                Location = seed.Location,
                QuantityDelta = seed.QuantityOnHand,
                QuantityOnHand = seed.QuantityOnHand,
                ReorderPoint = seed.ReorderPoint,
                OnOrder = seed.OnOrder,
                OccurredOnUtc = seed.LastMovementUtc,
                SourceReference = "seed",
                Audit = new AuditTrail
                {
                    CreatedOnUtc = nowUtc,
                    CreatedBy = "inventory-seed",
                    SourceModule = "Core.Inventory"
                }
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<InventorySeedItem> SeedData => new List<InventorySeedItem>
    {
        new("MED-0001", "Sterile Gloves", "Main Pharmacy", 420m, 200m, 120m, DateTime.UtcNow.AddDays(-2)),
        new("MED-0002", "IV Sets 500ml", "Main Pharmacy", 180m, 160m, 90m, DateTime.UtcNow.AddDays(-2)),
        new("MED-0003", "Rapid Test Kits", "Satellite Clinic", 60m, 80m, 200m, DateTime.UtcNow.AddDays(-1)),
        new("CON-0101", "Nitrile Gloves", "Satellite Clinic", 95m, 120m, 60m, DateTime.UtcNow.AddDays(-1)),
        new("SUP-0201", "Thermal Paper Rolls", "Front Store", 35m, 50m, 120m, DateTime.UtcNow.AddDays(-3)),
        new("SUP-0202", "Receipt Printer Ink", "Front Store", 18m, 40m, 80m, DateTime.UtcNow.AddDays(-3)),
        new("EQP-0301", "Ultrasound Gel", "Radiology", 75m, 40m, 30m, DateTime.UtcNow.AddDays(-1)),
        new("EQP-0302", "Lead Aprons", "Radiology", 12m, 10m, 4m, DateTime.UtcNow.AddDays(-4)),
        new("GEN-0401", "Coffee Beans", "Cafeteria", 48m, 30m, 20m, DateTime.UtcNow.AddDays(-1)),
        new("GEN-0402", "Disposable Cups", "Cafeteria", 600m, 300m, 500m, DateTime.UtcNow.AddDays(-2))
    };

    private sealed record InventorySeedItem(
        string Sku,
        string ItemName,
        string Location,
        decimal QuantityOnHand,
        decimal ReorderPoint,
        decimal OnOrder,
        DateTime LastMovementUtc);
}
