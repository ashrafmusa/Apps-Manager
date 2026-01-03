using System;
using System.Linq;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using ExcellCore.Module.Extensions.Sync.Models;
using ExcellCore.Module.Extensions.Sync.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcellCore.Tests;

public sealed class DeltaSyncProviderTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly IDeltaSyncProvider _provider;

    public DeltaSyncProviderTests()
    {
        _factory = new TestSqliteContextFactory();
        var resolver = new ConflictResolverService(_factory);
        _provider = new DeltaSyncProvider(_factory, resolver);
    }

    [Fact]
    public async Task CaptureLocalChangesAsync_ReturnsLedgerEntriesAsDeltas()
    {
        var now = DateTime.UtcNow;
        await using (var context = await _factory.CreateDbContextAsync())
        {
            context.SyncChangeLedgerEntries.Add(new SyncChangeLedgerEntry
            {
                SyncChangeLedgerEntryId = Guid.NewGuid(),
                AggregateType = "Agreement",
                AggregateId = Guid.NewGuid(),
                FieldName = "Status",
                PreviousValue = "Draft",
                NewValue = "Pending",
                OriginSiteId = "site-a",
                OriginDeviceId = "node-1",
                ObservedOnUtc = now,
                VectorClockJson = VectorClockSerializer.Serialize(new VectorClockStamp(new System.Collections.Generic.Dictionary<string, long>
                {
                    ["site-a"] = now.Ticks
                })),
                Audit = new AuditTrail { CreatedBy = "tests", SourceModule = "Tests" }
            });

            await context.SaveChangesAsync();
        }

        var deltas = await _provider.CaptureLocalChangesAsync(now.AddMinutes(-5));

        Assert.Single(deltas);
        var delta = deltas.Single();
        Assert.Equal("Agreement", delta.AggregateType);
        Assert.Equal("Status", delta.Changes.Single().FieldName);
        Assert.Equal("Pending", delta.Changes.Single().NewValue);
        Assert.Equal("site-a", delta.Origin.SiteId);
    }

    [Fact]
    public async Task ApplyIncomingDeltasAsync_AppendsLedger_WhenClockDominates()
    {
        var aggregateId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var dominantDelta = new SyncDelta(
            "Agreement",
            aggregateId,
            new[] { new SyncFieldChange("Status", "Pending", "Draft") },
            new VectorClockStamp(new System.Collections.Generic.Dictionary<string, long> { ["site-a"] = now.Ticks }),
            new SyncOrigin("site-a", "node-1", now));

        await _provider.ApplyIncomingDeltasAsync(new[] { dominantDelta });

        await using var context = await _factory.CreateDbContextAsync();
        var ledgerCount = await context.SyncChangeLedgerEntries.CountAsync();
        var state = await context.SyncStates.FirstOrDefaultAsync(s => s.AggregateId == aggregateId && s.AggregateType == "Agreement");

        Assert.Equal(1, ledgerCount);
        Assert.NotNull(state);

        var dominatedDelta = new SyncDelta(
            "Agreement",
            aggregateId,
            new[] { new SyncFieldChange("Status", "Draft", "Pending") },
            new VectorClockStamp(new System.Collections.Generic.Dictionary<string, long> { ["site-a"] = now.AddMinutes(-10).Ticks }),
            new SyncOrigin("site-a", "node-1", now.AddMinutes(-10)));

        await _provider.ApplyIncomingDeltasAsync(new[] { dominatedDelta });

        var ledgerCountAfter = await context.SyncChangeLedgerEntries.CountAsync();
        Assert.Equal(2, ledgerCountAfter); // triage entry added for dominated delta

        var triage = await context.SyncChangeLedgerEntries.FirstOrDefaultAsync(e => e.FieldName == "__triage__");
        Assert.NotNull(triage);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
