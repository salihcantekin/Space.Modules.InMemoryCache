using Space.Abstraction.Modules.Contracts;
using System.Threading.Tasks;

namespace Space.Modules.InMemoryCache.Cache;

public interface ICacheModuleProvider : IModuleProvider
{


    public abstract string GetKey<TRequest>(TRequest request);

    public abstract ValueTask Store<TResponse>(string key, TResponse response, CacheModuleConfig config);

    public abstract bool TryGet<TResponse>(string key, out TResponse response, CacheModuleConfig config);

    // Manual eviction APIs (optional for implementers)
    // Default no-op implementations to preserve compatibility
    public virtual bool Remove(string key) => false;

    public virtual void Clear() { }
}

