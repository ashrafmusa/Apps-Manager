using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Module.Extensions.Sync.Models;
using ExcellCore.Module.Extensions.Sync.Services;
using Xunit;

namespace ExcellCore.Tests;

public sealed class SyncTransportAdapterTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task ExportAsync_SerializesEnvelopeWithMetadata()
    {
        var nowUtc = DateTime.UtcNow;
        var delta = CreateDelta(nowUtc);
        var provider = new StubDeltaSyncProvider
        {
            CaptureResult = new List<SyncDelta> { delta }
        };

        var adapter = new JsonSyncTransportAdapter(provider, "SiteA", "DeviceX");

        var payload = await adapter.ExportAsync(nowUtc.AddHours(-1));
        var envelope = JsonSerializer.Deserialize<SyncTransportEnvelope>(payload, SerializerOptions);

        Assert.NotNull(envelope);
        Assert.Equal("SiteA", envelope!.SourceSiteId);
        Assert.Equal("DeviceX", envelope.SourceDeviceId);
        Assert.Single(envelope.Deltas);
        Assert.Equal(delta.AggregateId, envelope.Deltas[0].AggregateId);
    }

    [Fact]
    public async Task ImportAsync_InvokesProviderAndReturnsResult()
    {
        var delta = CreateDelta(DateTime.UtcNow);
        var envelope = new SyncTransportEnvelope("RemoteSite", "DeviceY", DateTime.UtcNow.AddMinutes(-5), new List<SyncDelta> { delta });
        var payload = JsonSerializer.Serialize(envelope, SerializerOptions);
        var provider = new StubDeltaSyncProvider();
        var adapter = new JsonSyncTransportAdapter(provider, "SiteA");

        var result = await adapter.ImportAsync(payload);

        Assert.Equal(1, result.AppliedCount);
        Assert.Equal("RemoteSite", result.SourceSiteId);
        Assert.Single(provider.AppliedDeltas);
        Assert.Equal(delta.AggregateId, provider.AppliedDeltas[0].AggregateId);
    }

    private static SyncDelta CreateDelta(DateTime capturedOnUtc)
    {
        return new SyncDelta(
            "Agreement",
            Guid.NewGuid(),
            new List<SyncFieldChange>
            {
                new("Status", "Active", "Draft")
            },
            new VectorClockStamp(new Dictionary<string, long> { ["SiteA"] = 1 }),
            new SyncOrigin("SiteA", "Device1", DateTime.SpecifyKind(capturedOnUtc, DateTimeKind.Utc)));
    }

    private sealed class StubDeltaSyncProvider : IDeltaSyncProvider
    {
        public List<SyncDelta> CaptureResult { get; set; } = new();
        public List<SyncDelta> AppliedDeltas { get; } = new();

        public Task ApplyIncomingDeltasAsync(IEnumerable<SyncDelta> deltas, CancellationToken cancellationToken = default)
        {
            AppliedDeltas.AddRange(deltas);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncDelta>> CaptureLocalChangesAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SyncDelta>>(CaptureResult);
        }

        public Task<IReadOnlyList<SyncTriageItem>> GetTriageAsync(int take = 50, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SyncTriageItem>>(Array.Empty<SyncTriageItem>());
        }
    }
}
