using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using ExcellCore.Module.Extensions.Sync.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Reflection;

namespace ExcellCore.Module.Extensions.Sync.Services;

public sealed class DeltaSyncProvider : IDeltaSyncProvider
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly IConflictResolverService _conflictResolver;

    public DeltaSyncProvider(IDbContextFactory<ExcellCoreContext> contextFactory, IConflictResolverService conflictResolver)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _conflictResolver = conflictResolver ?? throw new ArgumentNullException(nameof(conflictResolver));
    }

    public async Task<IReadOnlyList<SyncDelta>> CaptureLocalChangesAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        if (sinceUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Capture window must be expressed in UTC.", nameof(sinceUtc));
        }

        var ledgerEntries = await context.SyncChangeLedgerEntries
            .AsNoTracking()
            .Where(e => e.ObservedOnUtc >= sinceUtc)
            .OrderBy(e => e.ObservedOnUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var deltas = new List<SyncDelta>(ledgerEntries.Count);
        foreach (var entry in ledgerEntries)
        {
            var vectorClock = VectorClockSerializer.Deserialize(entry.VectorClockJson);
            var observedUtc = entry.ObservedOnUtc.Kind == DateTimeKind.Utc
                ? entry.ObservedOnUtc
                : DateTime.SpecifyKind(entry.ObservedOnUtc, DateTimeKind.Utc);
            var origin = new SyncOrigin(entry.OriginSiteId, entry.OriginDeviceId, observedUtc);
            var changes = new List<SyncFieldChange>
            {
                new SyncFieldChange(entry.FieldName, entry.NewValue, entry.PreviousValue)
            };

            deltas.Add(new SyncDelta(entry.AggregateType, entry.AggregateId, changes, vectorClock, origin));
        }

        return deltas;
    }

    public async Task ApplyIncomingDeltasAsync(IEnumerable<SyncDelta> deltas, CancellationToken cancellationToken = default)
    {
        if (deltas is null)
        {
            throw new ArgumentNullException(nameof(deltas));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        foreach (var delta in deltas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _conflictResolver.ResolveAsync(delta, cancellationToken).ConfigureAwait(false);
            switch (result.Outcome)
            {
                case ConflictResolutionOutcome.Applied:
                    var materialized = await TryMaterializeAsync(context, delta, cancellationToken).ConfigureAwait(false);
                    if (materialized)
                    {
                        await AppendLedgerAsync(context, delta, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await EnqueueTriageAsync(context, delta, "Unable to apply changes to target aggregate", cancellationToken).ConfigureAwait(false);
                    }
                    break;
                case ConflictResolutionOutcome.Deferred:
                    await EnqueueTriageAsync(context, delta, "Vector clock dominated or conflict requires operator triage", cancellationToken).ConfigureAwait(false);
                    break;
                case ConflictResolutionOutcome.Unhandled:
                    await EnqueueTriageAsync(context, delta, "Unhandled resolution outcome", cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown conflict resolution outcome: {result.Outcome}");
            }
        }
    }

    public async Task<IReadOnlyList<SyncTriageItem>> GetTriageAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var entries = await context.SyncChangeLedgerEntries
            .AsNoTracking()
            .Where(e => e.FieldName == "__triage__")
            .OrderByDescending(e => e.ObservedOnUtc)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<SyncTriageItem>(entries.Count);
        foreach (var entry in entries)
        {
            var payload = DeserializeTriagePayload(entry.NewValue);
            var changes = payload?.Delta?.Changes ?? Array.Empty<SyncFieldChange>();
            items.Add(new SyncTriageItem(
                entry.AggregateType,
                entry.AggregateId,
                payload?.Reason ?? "triage",
                entry.OriginSiteId,
                entry.OriginDeviceId,
                entry.ObservedOnUtc,
                changes.ToList(),
                payload?.RiskLevel));
        }

        return items;
    }

    private static async Task AppendLedgerAsync(ExcellCoreContext context, SyncDelta delta, CancellationToken cancellationToken)
    {
        var clockJson = VectorClockSerializer.Serialize(delta.VectorClock);
        var observedOnUtc = delta.Origin.CapturedOnUtc == DateTime.MinValue
            ? DateTime.UtcNow
            : DateTime.SpecifyKind(delta.Origin.CapturedOnUtc, DateTimeKind.Utc);

        foreach (var change in delta.Changes)
        {
            var entry = new SyncChangeLedgerEntry
            {
                SyncChangeLedgerEntryId = Guid.NewGuid(),
                AggregateType = delta.AggregateType,
                AggregateId = delta.AggregateId,
                FieldName = change.FieldName,
                PreviousValue = change.PreviousValue is null ? null : JsonSerializer.Serialize(change.PreviousValue),
                NewValue = change.NewValue is null ? null : JsonSerializer.Serialize(change.NewValue),
                OriginSiteId = delta.Origin.SiteId ?? string.Empty,
                OriginDeviceId = delta.Origin.DeviceId ?? string.Empty,
                ObservedOnUtc = observedOnUtc,
                VectorClockJson = clockJson,
                Audit = new AuditTrail
                {
                    CreatedBy = "sync-inbound",
                    SourceModule = "Extensions.Sync"
                }
            };

            context.SyncChangeLedgerEntries.Add(entry);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnqueueTriageAsync(ExcellCoreContext context, SyncDelta delta, string reason, CancellationToken cancellationToken)
    {
        var clockJson = VectorClockSerializer.Serialize(delta.VectorClock);
        var payload = new SyncTriagePayload(reason, delta, DateTime.UtcNow, ResolveRiskLevel(delta));
        var entry = new SyncChangeLedgerEntry
        {
            SyncChangeLedgerEntryId = Guid.NewGuid(),
            AggregateType = delta.AggregateType,
            AggregateId = delta.AggregateId,
            FieldName = "__triage__",
            PreviousValue = null,
            NewValue = JsonSerializer.Serialize(payload),
            OriginSiteId = delta.Origin.SiteId ?? string.Empty,
            OriginDeviceId = delta.Origin.DeviceId ?? string.Empty,
            ObservedOnUtc = delta.Origin.CapturedOnUtc,
            VectorClockJson = clockJson,
            Audit = new AuditTrail
            {
                CreatedBy = "sync-triage",
                SourceModule = "Extensions.Sync"
            }
        };

        context.SyncChangeLedgerEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? ResolveRiskLevel(SyncDelta delta)
    {
        // Simple heuristic: if any change touches Status and sets it to Draft/Denied, mark Warning; else Info.
        var statusChange = delta.Changes.FirstOrDefault(c => c.FieldName.Equals("Status", StringComparison.OrdinalIgnoreCase));
        if (statusChange is null)
        {
            return "Info";
        }

        var statusValue = statusChange.NewValue?.ToString() ?? string.Empty;
        if (statusValue.Contains("denied", StringComparison.OrdinalIgnoreCase) || statusValue.Contains("rejected", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        return "Info";
    }

    private static SyncTriagePayload? DeserializeTriagePayload(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SyncTriagePayload>(value);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> TryMaterializeAsync(ExcellCoreContext context, SyncDelta delta, CancellationToken cancellationToken)
    {
        switch (delta.AggregateType)
        {
            case "Agreement":
                return await ApplyToAggregateAsync(context, context.Agreements, delta, cancellationToken).ConfigureAwait(false);
            case "AgreementApproval":
                return await ApplyToAggregateAsync(context, context.AgreementApprovals, delta, cancellationToken).ConfigureAwait(false);
            case "Party":
                return await ApplyToAggregateAsync(context, context.Parties, delta, cancellationToken).ConfigureAwait(false);
            default:
                return false;
        }
    }

    private static async Task<bool> ApplyToAggregateAsync<TEntity>(ExcellCoreContext context, DbSet<TEntity> set, SyncDelta delta, CancellationToken cancellationToken)
        where TEntity : class
    {
        var keyProp = ResolveKeyProperty(typeof(TEntity), delta.AggregateType);
        if (keyProp is null)
        {
            return false;
        }

        var entity = await set.FirstOrDefaultAsync(e => EF.Property<Guid>(e, keyProp.Name) == delta.AggregateId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        var appliedAny = false;
        foreach (var change in delta.Changes)
        {
            appliedAny |= ApplyProperty(entity, change);
        }

        if (appliedAny)
        {
            ApplyAudit(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return appliedAny;
    }

    private static PropertyInfo? ResolveKeyProperty(Type entityType, string aggregateType)
    {
        var nameMatch = entityType.GetProperty(FormattableString.Invariant($"{aggregateType}Id"), BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (nameMatch is not null)
        {
            return nameMatch;
        }

        var genericId = entityType.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (genericId is not null && genericId.PropertyType == typeof(Guid))
        {
            return genericId;
        }

        return entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(p => p.PropertyType == typeof(Guid) && p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ApplyProperty(object entity, SyncFieldChange change)
    {
        var type = entity.GetType();
        var prop = type.GetProperty(change.FieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (prop is null || !prop.CanWrite)
        {
            return false;
        }

        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        var value = NormalizeValue(change.NewValue, targetType);
        if (value is null && targetType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) is null)
        {
            return false;
        }

        prop.SetValue(entity, value);
        return true;
    }

    private static object? NormalizeValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String when targetType == typeof(Guid) => Guid.TryParse(element.GetString(), out var g) ? g : Guid.Empty,
                JsonValueKind.String when targetType == typeof(DateTime) => DateTime.TryParse(element.GetString(), out var d) ? d : DateTime.MinValue,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when targetType == typeof(int) && element.TryGetInt32(out var i) => i,
                JsonValueKind.Number when targetType == typeof(decimal) && element.TryGetDecimal(out var dec) => dec,
                JsonValueKind.Number when targetType == typeof(double) && element.TryGetDouble(out var dbl) => dbl,
                JsonValueKind.True or JsonValueKind.False when targetType == typeof(bool) => element.GetBoolean(),
                _ => element.ToString()
            };
        }

        if (targetType == typeof(Guid) && Guid.TryParse(value.ToString(), out var guid))
        {
            return guid;
        }

        if (targetType == typeof(DateTime) && DateTime.TryParse(value.ToString(), out var dt))
        {
            return dt;
        }

        if (targetType == typeof(DateTimeOffset) && DateTimeOffset.TryParse(value.ToString(), out var dto))
        {
            return dto;
        }

        if (targetType == typeof(int) && int.TryParse(value.ToString(), out var intVal))
        {
            return intVal;
        }

        if (targetType == typeof(decimal) && decimal.TryParse(value.ToString(), out var decVal))
        {
            return decVal;
        }

        if (targetType == typeof(double) && double.TryParse(value.ToString(), out var dblVal))
        {
            return dblVal;
        }

        if (targetType == typeof(bool) && bool.TryParse(value.ToString(), out var boolVal))
        {
            return boolVal;
        }

        if (targetType.IsEnum)
        {
            if (Enum.TryParse(targetType, value.ToString(), true, out var enumVal))
            {
                return enumVal;
            }
        }

        // Default to string conversion
        if (targetType == typeof(string))
        {
            return value.ToString();
        }

        return null;
    }

    private static void ApplyAudit(object entity)
    {
        var auditProp = entity.GetType().GetProperty("Audit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (auditProp?.CanRead is true)
        {
            if (auditProp.GetValue(entity) is not AuditTrail audit)
            {
                audit = new AuditTrail();
                auditProp.SetValue(entity, audit);
            }

            audit.ModifiedBy = "sync-materialize";
            audit.ModifiedOnUtc = DateTime.UtcNow;
        }
    }

    private sealed record SyncTriagePayload(string Reason, SyncDelta Delta, DateTime CreatedOnUtc, string? RiskLevel = null);
}
