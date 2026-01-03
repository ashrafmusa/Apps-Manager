using System;
using System.Collections.Generic;
using System.Text.Json;
using ExcellCore.Module.Extensions.Sync.Models;

namespace ExcellCore.Module.Extensions.Sync.Services;

public static class VectorClockSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(VectorClockStamp stamp)
    {
        if (stamp is null)
        {
            throw new ArgumentNullException(nameof(stamp));
        }

        return JsonSerializer.Serialize(stamp.Entries, Options);
    }

    public static VectorClockStamp Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return VectorClockStamp.Empty;
        }

        var dictionary = JsonSerializer.Deserialize<Dictionary<string, long>>(json, Options) ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        return new VectorClockStamp(dictionary);
    }
}
