using Space.Abstraction.Modules;
using System;

namespace Space.Modules.InMemoryCache.Cache;

public class CacheProfileOptions : ICacheSettingsProperties
{
    public TimeSpan TimeSpan { get; set; }
}
