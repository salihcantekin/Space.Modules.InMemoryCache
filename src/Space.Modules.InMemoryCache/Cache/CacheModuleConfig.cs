using Space.Abstraction.Modules.Contracts;
using System;

namespace Space.Modules.InMemoryCache.Cache;

public class CacheModuleConfig : IModuleConfig, ICacheSettingsProperties
{
    public TimeSpan TimeSpan { get; set; } = TimeSpan.FromHours(1);
}

