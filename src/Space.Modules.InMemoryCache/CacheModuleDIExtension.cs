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
        CacheModuleOptions opt = new();
        optionAction?.Invoke(opt);
        
        services.AddSingleton<IReadOnlyDictionary<string, CacheModuleOptions>>(opt.Profiles);
        services.AddSingleton<IModuleGlobalOptionsAccessor<CacheModuleOptions>>(sp => new ModuleGlobalOptionsAccessor<CacheModuleOptions>(opt.Profiles));
        services.AddSingleton<ICacheModuleProvider, InMemoryCacheModuleProvider>();

        return services;
    }
}


// TODO remove this once it is public in Space.Abstraction
public sealed class ModuleGlobalOptionsAccessor<TModuleOptions>(IReadOnlyDictionary<string, TModuleOptions> profiles) : IModuleGlobalOptionsAccessor<TModuleOptions>
    where TModuleOptions : BaseModuleOptions
{
    public IReadOnlyDictionary<string, TModuleOptions> Profiles { get; } = profiles ?? new Dictionary<string, TModuleOptions>();
}