using System;
using System.Linq;
using System.Threading.Tasks;
using ExcellCore.Domain.Entities;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using Xunit;

namespace ExcellCore.Tests;

public sealed class InventoryAnalyticsServiceTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly InventoryAnalyticsService _service;

    public InventoryAnalyticsServiceTests()
    {
        _factory = new TestSqliteContextFactory();
        _service = new InventoryAnalyticsService(_factory, new SequentialGuidGenerator(), enableSeeding: false);
    }

    [Fact]
    public async Task GetStockSnapshotAsync_SeedsAndReturnsItems()
    {
        var seededService = new InventoryAnalyticsService(_factory, new SequentialGuidGenerator(), enableSeeding: true);
        var snapshot = await seededService.GetStockSnapshotAsync();

        Assert.NotEmpty(snapshot.Items);
        Assert.All(snapshot.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Sku)));
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsStockOutAndShrinkage()
    {
        var nowUtc = DateTime.UtcNow;

        await using (var context = await _factory.CreateDbContextAsync())
        {
            await context.InventoryLedgerEntries.AddRangeAsync(
                new InventoryLedgerEntry
                {
                    InventoryLedgerEntryId = Guid.NewGuid(),
                    Sku = "MED-1001",
                    ItemName = "Test Med",
                    Location = "Pharmacy",
                    QuantityDelta = 100,
                    QuantityOnHand = 100,
                    ReorderPoint = 25,
                    OnOrder = 10,
                    OccurredOnUtc = nowUtc.AddHours(-12),
                    Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "tests" }
                },
                new InventoryLedgerEntry
                {
                    InventoryLedgerEntryId = Guid.NewGuid(),
                    Sku = "MED-1001",
                    ItemName = "Test Med",
                    Location = "Pharmacy",
                    QuantityDelta = -110,
                    QuantityOnHand = -10,
                    ReorderPoint = 25,
                    OnOrder = 0,
                    OccurredOnUtc = nowUtc.AddHours(-1),
                    Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "tests" }
                },
                new InventoryLedgerEntry
                {
                    InventoryLedgerEntryId = Guid.NewGuid(),
                    Sku = "SUP-2001",
                    ItemName = "Test Supply",
                    Location = "Warehouse",
                    QuantityDelta = 150,
                    QuantityOnHand = 150,
                    ReorderPoint = 30,
                    OnOrder = 20,
                    OccurredOnUtc = nowUtc.AddHours(-12),
                    Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "tests" }
                },
                new InventoryLedgerEntry
                {
                    InventoryLedgerEntryId = Guid.NewGuid(),
                    Sku = "SUP-2001",
                    ItemName = "Test Supply",
                    Location = "Warehouse",
                    QuantityDelta = -90,
                    QuantityOnHand = 60,
                    ReorderPoint = 30,
                    OnOrder = 10,
                    OccurredOnUtc = nowUtc.AddHours(-2),
                    Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "tests" }
                });

            await context.SaveChangesAsync();
        }

        var result = await _service.AnalyzeAsync(TimeSpan.FromHours(24));

        Assert.Contains(result.Signals, signal => signal.SignalType == "StockOutRisk");
        Assert.Contains(result.Signals, signal => signal.SignalType == "Shrinkage");
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
