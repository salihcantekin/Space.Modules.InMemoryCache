using Space.Abstraction.Modules;
using Space.Abstraction.Modules.Audit;
using Space.Abstraction.Modules.Contracts;
using System;

namespace Space.Modules.InMemoryCache.Cache;

public class CacheModuleOptions : ProfileModuleOptions<CacheModuleOptions>, ICacheSettingsProperties
{
    public TimeSpan TimeSpan { get; set; }
}
