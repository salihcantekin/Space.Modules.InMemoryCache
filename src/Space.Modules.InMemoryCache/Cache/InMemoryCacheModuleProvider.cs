using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace Space.Modules.InMemoryCache.Cache;

public sealed class InMemoryCacheModuleProvider(TimeProvider timeProvider) : ICacheModuleProvider
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly MemoryCache cache = new(new MemoryCacheOptions());
    

    public InMemoryCacheModuleProvider() : this(TimeProvider.System) { }

    // introduce cache entry model to manage expiration
    private sealed class CacheEntry
    {
        public object Value { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }

    // change dictionary to CacheEntry

    public string GetKey<TRequest>(TRequest request)
    {
        return request?.ToString();
    }

    public ValueTask Store<TResponse>(string key, TResponse response, CacheModuleConfig config)
    {
        var duration = config?.TimeSpan ?? TimeSpan.Zero;
        var now = timeProvider.GetUtcNow();
        var expiresAt = duration <= TimeSpan.Zero ? DateTimeOffset.MaxValue : now.Add(duration);

        var entry = new CacheEntry { Value = response!, ExpiresAt = expiresAt };

        if (duration > TimeSpan.Zero)
        {
            cache.Set(key, entry, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration
            });
        }
        else
        {
            // No explicit expiration, keep indefinitely in memory cache
            cache.Set(key, entry);
        }

        return default;
    }

    public bool TryGet<TResponse>(string key, out TResponse response, CacheModuleConfig config)
    {
        response = default;

        if (!cache.TryGetValue(key, out CacheEntry entry))
            return false;

        if (entry.ExpiresAt <= timeProvider.GetUtcNow())
        {
            // invalidate expired
            cache.Remove(key);
            return false;
        }

        response = (TResponse)entry.Value;

        return true;

    }

    // Manual eviction APIs
    public bool Remove(string key)
    {
        // MemoryCache.Remove does not indicate if a key existed; emulate by TryGet first
        var existed = cache.TryGetValue(key, out _);
        cache.Remove(key);
        return existed;
    }

    public void Clear()
    {
        // Compact 1.0 clears the entire cache
        if (cache is MemoryCache mc)
        {
            mc.Compact(1.0);
        }
    }
}