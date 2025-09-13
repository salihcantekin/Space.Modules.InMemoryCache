using System;

namespace Space.Modules.InMemoryCache.Cache;

/// <summary>
/// Common settings shared by Audit attribute/options/config.
/// </summary>
public interface ICacheSettingsProperties
{
    public TimeSpan TimeSpan { get; set; }
}
