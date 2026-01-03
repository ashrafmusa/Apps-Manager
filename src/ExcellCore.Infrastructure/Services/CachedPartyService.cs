using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ExcellCore.Infrastructure.Services;

internal sealed class CachedPartyService : IPartyService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly PartyService _inner;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _lookupKeys = new(StringComparer.OrdinalIgnoreCase);

    public CachedPartyService(PartyService inner, IMemoryCache cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public Task<IReadOnlyList<PartySummaryDto>> SearchAsync(string? searchTerm, CancellationToken cancellationToken = default)
    {
        return _inner.SearchAsync(searchTerm, cancellationToken);
    }

    public async Task<IReadOnlyList<PartyLookupResultDto>> LookupAsync(string? searchTerm, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cacheKey = BuildLookupCacheKey(searchTerm);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<PartyLookupResultDto>? cachedResults) && cachedResults is not null)
        {
            return cachedResults;
        }

        var results = await _inner.LookupAsync(searchTerm, cancellationToken).ConfigureAwait(false)
                      ?? Array.Empty<PartyLookupResultDto>();
        var snapshot = results as IReadOnlyList<PartyLookupResultDto> ?? results.ToArray();

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        };
        options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = (cacheKey, _, _, _) =>
            {
                if (cacheKey is string evictedKey)
                {
                    _lookupKeys.TryRemove(evictedKey, out _);
                }
            }
        });

        _cache.Set(cacheKey, snapshot, options);
        _lookupKeys.TryAdd(cacheKey, 0);

        return snapshot;
    }

    public Task<PartyDetailDto?> GetAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        return _inner.GetAsync(partyId, cancellationToken);
    }

    public async Task<PartyDetailDto> SaveAsync(PartyDetailDto detail, CancellationToken cancellationToken = default)
    {
        var result = await _inner.SaveAsync(detail, cancellationToken).ConfigureAwait(false);
        InvalidateLookupCache();
        return result;
    }

    private static string BuildLookupCacheKey(string? searchTerm)
    {
        return string.IsNullOrWhiteSpace(searchTerm)
            ? "lookup::warm"
            : FormattableString.Invariant($"lookup::{searchTerm.Trim().ToUpperInvariant()}");
    }

    private void InvalidateLookupCache()
    {
        foreach (var key in _lookupKeys.Keys.ToArray())
        {
            _cache.Remove(key);
            _lookupKeys.TryRemove(key, out _);
        }
    }
}
