using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Space.Modules.InMemoryCache.Cache;

public sealed class InMemoryCacheModuleProvider : ICacheModuleProvider
{
    // introduce cache entry model to manage expiration
    private sealed class CacheEntry
    {
        public object Value { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }

    // change dictionary to CacheEntry
    private readonly ConcurrentDictionary<string, CacheEntry> handlers = [];

    public string GetKey<TRequest>(TRequest request)
    {
        return request?.ToString();
    }

    public ValueTask Store<TResponse>(string key, TResponse response, CacheModuleConfig config)
    {
        var duration = config?.TimeSpan ?? TimeSpan.Zero;
        var expiresAt = duration <= TimeSpan.Zero ? DateTimeOffset.MaxValue : DateTimeOffset.UtcNow.Add(duration);

        handlers[key] = new CacheEntry { Value = response!, ExpiresAt = expiresAt };

        return default;
    }

    public bool TryGet<TResponse>(string key, out TResponse response, CacheModuleConfig config)
    {
        response = default;

        if (!handlers.TryGetValue(key, out var entry))
            return false;

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            // invalidate expired
            handlers.TryRemove(key, out _);
            return false;
        }

        response = (TResponse)entry.Value;

        return true;

    }
}