using System;
using System.Collections.Generic;

namespace ExcellCore.Module.Extensions.Sync.Models;

public sealed class VectorClockStamp
{
    [System.Text.Json.Serialization.JsonConstructor]
    public VectorClockStamp(IReadOnlyDictionary<string, long> entries)
    {
        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        Entries = new Dictionary<string, long>(entries, StringComparer.OrdinalIgnoreCase);
    }

    private VectorClockStamp(Dictionary<string, long> entries, bool ownsInstance)
    {
        Entries = ownsInstance ? entries : new Dictionary<string, long>(entries, StringComparer.OrdinalIgnoreCase);
    }

    public static VectorClockStamp Empty { get; } = new(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase), true);

    public IReadOnlyDictionary<string, long> Entries { get; }

    public long GetValueOrDefault(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            throw new ArgumentException("Site id is required.", nameof(siteId));
        }

        return Entries.TryGetValue(siteId, out var value) ? value : 0L;
    }

    public VectorClockComparison Compare(VectorClockStamp other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        var greater = false;
        var less = false;

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Entries.Keys)
        {
            keys.Add(entry);
        }
        foreach (var entry in other.Entries.Keys)
        {
            keys.Add(entry);
        }

        foreach (var key in keys)
        {
            var left = GetValueOrDefault(key);
            var right = other.GetValueOrDefault(key);

            if (left > right)
            {
                greater = true;
            }
            else if (left < right)
            {
                less = true;
            }

            if (greater && less)
            {
                return VectorClockComparison.Concurrent;
            }
        }

        if (!greater && !less)
        {
            return VectorClockComparison.Equal;
        }

        if (greater)
        {
            return VectorClockComparison.Dominates;
        }

        return VectorClockComparison.Dominated;
    }

    public VectorClockStamp Merge(VectorClockStamp other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        var merged = new Dictionary<string, long>(Entries, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in other.Entries)
        {
            if (merged.TryGetValue(kvp.Key, out var existing))
            {
                merged[kvp.Key] = Math.Max(existing, kvp.Value);
            }
            else
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        return new VectorClockStamp(merged, true);
    }
}

public enum VectorClockComparison
{
    Equal,
    Dominates,
    Dominated,
    Concurrent
}
