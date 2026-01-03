using System;
using System.Collections.Generic;
using System.Linq;

namespace ExcellCore.Module.Extensions.Sync.Models;

public sealed record SyncTriageItem(
    string AggregateType,
    Guid AggregateId,
    string Reason,
    string? OriginSiteId,
    string? OriginDeviceId,
    DateTime ObservedOnUtc,
    IReadOnlyList<SyncFieldChange> Changes,
    string? RiskLevel = null)
{
    public string ObservedDisplay => DateTime.SpecifyKind(ObservedOnUtc, DateTimeKind.Utc).ToLocalTime().ToString("g");
    public string ChangeSummary => Changes.Count == 0
        ? string.Empty
        : string.Join(", ", Changes.Select(c => c.FieldName));
}
