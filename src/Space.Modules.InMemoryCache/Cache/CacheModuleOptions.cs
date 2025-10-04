using Space.Abstraction.Modules;
using Space.Abstraction.Modules.Audit;
using Space.Abstraction.Modules.Contracts;
using System;

namespace Space.Modules.InMemoryCache.Cache;

// Root options container for profile-based configuration. No per-profile properties here.
public class CacheModuleOptions : ProfileOnlyModuleOptions<CacheProfileOptions>
{
}
