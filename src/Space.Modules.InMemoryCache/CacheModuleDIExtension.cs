using Microsoft.Extensions.DependencyInjection;
using Space.Abstraction.Modules;
using Space.Abstraction.Modules.Audit;
using Space.Abstraction.Modules.Contracts;
using Space.Modules.InMemoryCache.Cache;
using System;
using System.Collections.Generic;

namespace Space.Modules.InMemoryCache;

public static class CacheModuleDependencyInjectionExtensions
{
    public static IServiceCollection AddSpaceInMemoryCache(this IServiceCollection services, Action<CacheModuleOptions> optionAction = null)
    {
        AddDefaultsInternal(services, optionAction);
        services.AddSingleton<ICacheModuleProvider, InMemoryCacheModuleProvider>();
        return services;
    }

    public static IServiceCollection AddSpaceInMemoryCache<TCustomCacheProvider>(this IServiceCollection services, Action<CacheModuleOptions> optionAction = null)
        where TCustomCacheProvider : class, ICacheModuleProvider
    {
        AddDefaultsInternal(services, optionAction);
        services.AddSingleton<ICacheModuleProvider, TCustomCacheProvider>();
        return services;
    }

    private static void AddDefaultsInternal(IServiceCollection services, Action<CacheModuleOptions> optionAction)
    {
        CacheModuleOptions opt = new();
        optionAction?.Invoke(opt);

        // Register profiles dictionary and accessor for profile type
        services.AddSingleton<IReadOnlyDictionary<string, CacheProfileOptions>>(opt.Profiles);
        services.AddSingleton<IModuleGlobalOptionsAccessor<CacheProfileOptions>>(sp => new ModuleGlobalOptionsAccessor<CacheProfileOptions>(opt.Profiles));

        // Time source
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        foreach (var profile in opt.Profiles.Values)
        {
            ValidateOptions(profile);
        }
    }

    private static void ValidateOptions(CacheProfileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.TimeSpan < TimeSpan.Zero)
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
            throw new ArgumentOutOfRangeException(nameof(options.TimeSpan), "Duration must be non-negative.");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
    }
}