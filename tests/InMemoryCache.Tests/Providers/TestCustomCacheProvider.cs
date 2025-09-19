using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Space.Modules.InMemoryCache.Cache;

namespace InMemoryCache.Tests.Providers;

public sealed class TestCustomCacheProvider(TimeProvider timeProvider) : ICacheModuleProvider
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, (object Value, DateTimeOffset Expire)> store = new();
    private readonly object gate = new();

    public int StoreCount { get; private set; }
    public int TryGetCount { get; private set; }
    public string LastStoredKey { get; private set; }
    public string LastTryGetKey { get; private set; }

    public List<(string Method, string Key, TimeSpan Ttl)> Calls { get; } = [];

    public string GetKey<TRequest>(TRequest request)
    {
        var key = request?.ToString();
        lock (gate) Calls.Add(("GetKey", key, default));
        return key;
    }

    public ValueTask Store<TResponse>(string key, TResponse response, CacheModuleConfig config)
    {
        StoreCount++;
        LastStoredKey = key;
        var ttl = config?.TimeSpan ?? TimeSpan.Zero;
        var now = timeProvider.GetUtcNow();
        var exp = ttl <= TimeSpan.Zero ? DateTimeOffset.MaxValue : now.Add(ttl);
        store[key] = (response!, exp);
        lock (gate) Calls.Add(("Store", key, ttl));
        return default;
    }

    public bool TryGet<TResponse>(string key, out TResponse response, CacheModuleConfig config)
    {
        TryGetCount++;
        LastTryGetKey = key;
        response = default;
        var ttl = config?.TimeSpan ?? TimeSpan.Zero;
        lock (gate) Calls.Add(("TryGet", key, ttl));

        if (!store.TryGetValue(key, out var tuple))
            return false;

        if (tuple.Expire <= timeProvider.GetUtcNow())
        {
            store.TryRemove(key, out _);
            return false;
        }

        response = (TResponse)tuple.Value;
        return true;
    }

    public void ClearCalls()
    {
        lock (gate) Calls.Clear();
    }

    // Manual eviction support for tests
    public bool Remove(string key)
    {
        lock (gate) Calls.Add(("Remove", key, default));
        return store.TryRemove(key, out _);
    }

    public void Clear()
    {
        lock (gate) Calls.Add(("Clear", string.Empty, default));
        store.Clear();
    }
}
