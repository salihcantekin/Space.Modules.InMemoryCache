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

        services.AddSingleton<IReadOnlyDictionary<string, CacheModuleOptions>>(opt.Profiles);
        services.AddSingleton<IModuleGlobalOptionsAccessor<CacheModuleOptions>>(sp => new ModuleGlobalOptionsAccessor<CacheModuleOptions>(opt.Profiles));
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        foreach (var profile in opt.Profiles.Values)
        {
            ValidateOptions(profile);
        }
    }

    private static void ValidateOptions(CacheModuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TimeSpan < TimeSpan.Zero)
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
            throw new ArgumentOutOfRangeException(nameof(options.TimeSpan), "Duration must be non-negative.");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
    }

}


// TODO remove this once it is public in Space.Abstraction
public sealed class ModuleGlobalOptionsAccessor<TModuleOptions>(IReadOnlyDictionary<string, TModuleOptions> profiles) : IModuleGlobalOptionsAccessor<TModuleOptions>
    where TModuleOptions : BaseModuleOptions
{
    public IReadOnlyDictionary<string, TModuleOptions> Profiles { get; } = profiles ?? new Dictionary<string, TModuleOptions>();
}