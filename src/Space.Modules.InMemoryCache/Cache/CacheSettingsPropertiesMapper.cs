using System;
using System.Collections.Generic;

namespace Space.Modules.InMemoryCache.Cache;

internal static class CacheSettingsPropertiesMapper
{
    internal static Dictionary<string, object> ToDictionary(ICacheSettingsProperties src)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (src == null)
            return dict;

        if (src.TimeSpan > TimeSpan.Zero)
            dict[nameof(ICacheSettingsProperties.TimeSpan)] = src.TimeSpan;

        return dict;
    }

    internal static void ApplyTo(ICacheSettingsProperties target, IReadOnlyDictionary<string, object> props)
    {
        if (target == null || props == null)
            return;

        // Prefer non-zero TimeSpan (e.g., from profile options)
        if (props.TryGetValue(nameof(ICacheSettingsProperties.TimeSpan), out object lvl) && lvl is TimeSpan ts && ts > TimeSpan.Zero)
            target.TimeSpan = ts;

        // Attribute may provide Duration (seconds) instead of TimeSpan
        if (props.TryGetValue(nameof(CacheModuleAttribute.Duration), out var durObj) && durObj is not null)
        {
            var seconds = durObj switch
            {
                int i => i,
                long l => (int)l,
                string s when int.TryParse(s, out var i2) => i2,
                _ => 0
            };
            if (seconds > 0)
            {
                target.TimeSpan = TimeSpan.FromSeconds(seconds);
            }
        }
    }
}