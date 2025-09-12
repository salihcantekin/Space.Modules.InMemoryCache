using Space.Abstraction.Modules.Contracts;
using System;

namespace Space.Modules.InMemoryCache.Cache;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class CacheModuleAttribute : Attribute, ISpaceModuleAttribute
{
    public string Profile { get; set; } = "Default";

    public int Duration { get; set; } = 0; // default to 0 seconds; profile/global options decide
}
