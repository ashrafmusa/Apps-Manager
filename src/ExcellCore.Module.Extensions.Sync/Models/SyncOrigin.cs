using System;

namespace ExcellCore.Module.Extensions.Sync.Models;

public sealed class SyncOrigin
{
    [System.Text.Json.Serialization.JsonConstructor]
    public SyncOrigin(string siteId, string? deviceId, DateTime capturedOnUtc)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            throw new ArgumentException("Site id is required.", nameof(siteId));
        }

        if (capturedOnUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Timestamps must be expressed in UTC.", nameof(capturedOnUtc));
        }

        SiteId = siteId;
        DeviceId = deviceId;
        CapturedOnUtc = capturedOnUtc;
    }

    public string SiteId { get; }
    public string? DeviceId { get; }
    public DateTime CapturedOnUtc { get; }
}
