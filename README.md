# Space.Modules.InMemoryCache

In-memory cache module for the Space framework. Adds a caching layer in the handler pipeline when a method is annotated.

## Status & Packages

| CI | Branch | Status |
|---|---|---|
| Prod (Stable Publish) | `master` | [![Prod CI](https://github.com/salihcantekin/Space.Modules.InMemoryCache/actions/workflows/prod-ci.yml/badge.svg?branch=master)](https://github.com/salihcantekin/Space.Modules.InMemoryCache/actions/workflows/prod-ci.yml) |
| Dev (Preview Publish) | `dev` | [![Dev CI](https://github.com/salihcantekin/Space.Modules.InMemoryCache/actions/workflows/dev-ci.yml/badge.svg?branch=dev)](https://github.com/salihcantekin/Space.Modules.InMemoryCache/actions/workflows/dev-ci.yml) |
| Validation (PR / Feature) | PRs -> `dev` / `master` | [![Validation Build](https://github.com/salihcantekin/Space.Modules.InMemoryCache/actions/workflows/validation-build.yml/badge.svg)](https://github.com/salihcantekin/Space.Modules.InMemoryCache/actions/workflows/validation-build.yml) |
| Coverage (Tests) | Release | [![Coverage](https://img.shields.io/badge/coverage-report-blue)](https://github.com/salihcantekin/Space.Modules.InMemoryCache/actions/workflows/validation-build.yml) |

## NuGet Packages

| Package | Stable | Preview | Downloads | Description |
|---|---:|---:|---:|---|
| [Space.Modules.InMemoryCache](https://www.nuget.org/packages/Space.Modules.InMemoryCache) | [![Stable](https://img.shields.io/nuget/v/Space.Modules.InMemoryCache.svg)](https://www.nuget.org/packages/Space.Modules.InMemoryCache) | [![Preview](https://img.shields.io/nuget/vpre/Space.Modules.InMemoryCache.svg)](https://www.nuget.org/packages/Space.Modules.InMemoryCache) | [![Downloads](https://img.shields.io/nuget/dt/Space.Modules.InMemoryCache.svg)](https://www.nuget.org/packages/Space.Modules.InMemoryCache) | In-memory caching module + attribute integration |

## NuGet
- Package page: https://www.nuget.org/packages/Space.Modules.InMemoryCache
- Target Frameworks: .NET 8

Install (stable):
```bash
 dotnet add package Space.Modules.InMemoryCache
```

Install (preview):
```bash
 dotnet add package Space.Modules.InMemoryCache --prerelease
```

## Build & CI
- Prod CI (release pipeline): [link](https://github.com/salihcantekin/Space.Modules.InMemoryCache/actions/workflows/prod-ci.yml)
- Dev CI (dev branch): [link](https://github.com/salihcantekin/Space.Modules.InMemoryCache/actions/workflows/dev-ci.yml)
- Validation Build (feature branches & PRs): [link](https://github.com/salihcantekin/Space.Modules.InMemoryCache/actions/workflows/validation-build.yml)

> Coverage report is generated as `coverage.cobertura.xml` in CI (uploaded as artifact). You can wire Codecov/Coveralls later for a percentage badge if desired.

## Registration
If the package is referenced, `AddSpace()` will detect the module.

- Default provider registration
```csharp
services.AddSpace();
services.AddSpaceInMemoryCache(); // registers InMemoryCacheModuleProvider
```

- Generic overload with a custom provider type
```csharp
// Registers your own provider instead of the default
services.AddSpace();
services.AddSpaceInMemoryCache<MyCustomCacheProvider>();
```

- Generic overload + profile options
```csharp
services.AddSpace();
services.AddSpaceInMemoryCache<MyCustomCacheProvider>(opt =>
{
    // default profile used when attribute has no properties
    opt.WithDefaultProfile(p => p.TimeSpan = TimeSpan.FromMinutes(1));

    // named profile can be selected via [CacheModule(Profile = "fast")]
    opt.WithProfile("fast", p => p.TimeSpan = TimeSpan.FromSeconds(5));
});
```
Order does not matter relative to `AddSpace()`.

## Usage
Apply `[CacheModule]` on a handler method (together with `[Handle]`). The optional `Duration` (seconds) influences the internal config `TimeSpan`.
```csharp
public class UserQueries
{
    [Handle]
    [CacheModule(Duration = 60)] // seconds
    public ValueTask<List<UserDetail>> GetTopUsers(HandlerContext<int> ctx)
    {
        // access services via ctx.ServiceProvider
        // return cached value if present (module manages this)
    }
}
```

## Manual Eviction (invalidate cached entries)
There are two ways to remove cached entries when your data mutates:

- Remove a single key
  - Use `ICacheModuleProvider` from your handler/command.
  - Build the cache key of the related query using `GetKey(request)` and call `Remove(key)`.

- Clear all cache entries
  - Call `Clear()` on the provider (useful for admin/ops or wide reconfiguration).

Example: invalidate a cached query after a successful mutation
```csharp
public record GetUser(string Email) : IRequest<UserDto>;
public record UpdateUser(string Email, string NewId) : IRequest<Nothing>;

public class UserQueries
{
    [Handle]
    [CacheModule(Duration = 60)]
    public ValueTask<UserDto> Get(HandlerContext<GetUser> ctx)
        => ValueTask.FromResult(new UserDto($"{ctx.Request.Email}:{Guid.NewGuid()}"));
}

public class UserCommands(ICacheModuleProvider cache)
{
    [Handle]
    public ValueTask<Nothing> Update(HandlerContext<UpdateUser> ctx)
    {
        // do your state change first (DB/update etc.)
        // ...
        // then invalidate the related query cache
        var key = cache.GetKey(new GetUser(ctx.Request.Email));
        cache.Remove(key);
        return ValueTask.FromResult(Nothing.Value);
    }
}
```
Notes:
- Place eviction after a successful commit of the mutation.
- If multiple queries are affected, generate and remove all relevant keys.
- `Clear()` removes everything from the in-memory cache.

## Key generation
By default the provider uses `request.ToString()` as the cache key. For stable and readable keys, override `ToString()` on your request records/DTOs (or implement a custom provider). Avoid `GetHashCode()` for keys since hashes can collide and are not guaranteed to be stable across processes/versions.

## How It Works
- Source generator detects `[CacheModule]` usage and prepares module metadata.
- At runtime the module resolves an `ICacheModuleProvider` (default registered by `AddSpaceInMemoryCache`).
- The provider supplies keys and stores / retrieves values.
- Module order (`PipelineOrder = int.MinValue + 2`) ensures it runs before user pipelines but after any earlier system modules (e.g. Audit if introduced with lower order).

## Custom Provider
Implement `ICacheModuleProvider` to change key strategy or backing store (e.g. Redis):
```csharp
public sealed class RedisCacheModuleProvider : ICacheModuleProvider
{
    private readonly ConcurrentDictionary<string, object> store = new();

    public string GetKey<TRequest>(TRequest request) => request?.ToString();

    public ValueTask Store<TResponse>(string key, TResponse response, CacheModuleConfig cfg)
    { store[key] = response; return default; }

    public bool TryGet<TResponse>(string key, out TResponse response, CacheModuleConfig cfg)
    {
        response = default;
        if (!store.TryGetValue(key, out var obj)) return false;
        response = (TResponse)obj; return true;
    }

    // optional manual eviction
    public bool Remove(string key) => store.TryRemove(key, out _);
    public void Clear() => store.Clear();
}
```
Register it using the DI extension:
```csharp
services.AddSpaceInMemoryCache<RedisCacheModuleProvider>();
services.AddSpace();
```

## Configuration Mapping
`Duration` (int seconds) from the attribute populates `CacheModuleConfig.TimeSpan`. If omitted or < 0 it is treated as 0 (no expiration). Global and named profile values can also provide `TimeSpan`.

## Notes
- A module attribute augments its `[Handle]` method; it does not introduce its own method.
- Only handlers explicitly annotated with `[CacheModule]` are cached.

## Links
- Repo: https://github.com/salihcantekin/Space.Modules.InMemoryCache
- NuGet: https://www.nuget.org/packages/Space.Modules.InMemoryCache
- License: MIT
