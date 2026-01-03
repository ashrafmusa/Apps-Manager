using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Module.Extensions.Sync.Models;

namespace ExcellCore.Module.Extensions.Sync.Services;

public sealed class JsonSyncTransportAdapter : ISyncTransportAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IDeltaSyncProvider _deltaSyncProvider;
    private readonly string _sourceSiteId;
    private readonly string? _sourceDeviceId;

    public JsonSyncTransportAdapter(IDeltaSyncProvider deltaSyncProvider, string sourceSiteId, string? sourceDeviceId = null)
    {
        _deltaSyncProvider = deltaSyncProvider ?? throw new ArgumentNullException(nameof(deltaSyncProvider));

        if (string.IsNullOrWhiteSpace(sourceSiteId))
        {
            throw new ArgumentException("Source site id is required.", nameof(sourceSiteId));
        }

        _sourceSiteId = sourceSiteId;
        _sourceDeviceId = string.IsNullOrWhiteSpace(sourceDeviceId) ? null : sourceDeviceId;
    }

    public async Task<string> ExportAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var deltas = await _deltaSyncProvider.CaptureLocalChangesAsync(sinceUtc, cancellationToken).ConfigureAwait(false);
        var envelope = new SyncTransportEnvelope(_sourceSiteId, _sourceDeviceId, DateTime.UtcNow, deltas);
        return JsonSerializer.Serialize(envelope, SerializerOptions);
    }

    public async Task<SyncTransportResult> ImportAsync(string payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload cannot be empty.", nameof(payload));
        }

        var envelope = JsonSerializer.Deserialize<SyncTransportEnvelope>(payload, SerializerOptions)
                       ?? throw new InvalidOperationException("Unable to deserialize sync envelope.");

        await _deltaSyncProvider.ApplyIncomingDeltasAsync(envelope.Deltas, cancellationToken).ConfigureAwait(false);

        var appliedCount = envelope.Deltas?.Count ?? 0;
        var status = appliedCount == 0
            ? "No deltas applied"
            : $"Applied {appliedCount} delta(s) from {envelope.SourceSiteId}";

        return new SyncTransportResult(appliedCount, 0, DateTime.UtcNow, envelope.SourceSiteId, envelope.SourceDeviceId, status);
    }
}
