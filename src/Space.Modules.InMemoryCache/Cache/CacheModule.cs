using Microsoft.Extensions.DependencyInjection;
using Space.Abstraction;
using Space.Abstraction.Exceptions;
using Space.Abstraction.Modules;
using Space.Abstraction.Modules.Audit;
using Space.Abstraction.Modules.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Space.Modules.InMemoryCache.Cache;

// Temp. When this is available in Space.Abstraction, remove this.
internal static class ModuleConfigMerge
{
    public static Dictionary<string, object> Merge(
        IReadOnlyDictionary<string, object> defaultProperties,
        IReadOnlyDictionary<string, object> globalProfileProperties,
        IReadOnlyDictionary<string, object> attributeProperties)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        void AddRange(IReadOnlyDictionary<string, object> src)
        {
            if (src == null)
                return;

            foreach (var kv in src)
            {
                result[kv.Key] = kv.Value;
            }
        }

        AddRange(defaultProperties);          // lowest priority
        AddRange(globalProfileProperties);    // middle priority
        AddRange(attributeProperties);        // highest priority

        return result;
    }
}


[SpaceModule(ModuleAttributeType = typeof(CacheModuleAttribute))]
public class CacheModule(IServiceProvider serviceProvider) : SpaceModule(serviceProvider)
{
    private IModuleProvider moduleProvider;
    private readonly CacheModuleConfig cacheModuleConfig;

    public override int PipelineOrder => int.MinValue + 2;

    public override Type GetAttributeType()
    {
        return typeof(CacheModuleAttribute);
    }

    public override IModuleConfig GetModuleConfig(ModuleIdentifier moduleKey)
    {
        return GetOrAddConfig(moduleKey, () =>
        {
            IReadOnlyDictionary<string, object> defaultProps = new Dictionary<string, object>();

            var globalProfiles = GetGlobalProfiles();
            var requested = NormalizeProfileName(moduleKey.ProfileName);

            CacheModuleOptions profileOpt = null;
            if (globalProfiles != null)
            {
                profileOpt = globalProfiles.FirstOrDefault(kv => string.Equals(kv.Key, requested, StringComparison.OrdinalIgnoreCase)).Value;
                profileOpt ??= globalProfiles.FirstOrDefault(kv => string.Equals(kv.Key, ModuleConstants.DefaultProfileName, StringComparison.OrdinalIgnoreCase)).Value;
            }

            var globalProfileProps = ExtractProfileProperties(profileOpt);

            var attributeConfig = ServiceProvider.GetKeyedService<ModuleConfig>(moduleKey);
            var attributeProps = attributeConfig?.GetAllModuleProperties();

            var merged = ModuleConfigMerge.Merge(defaultProps, globalProfileProps, attributeProps);

            var cfg = new CacheModuleConfig();
            CacheSettingsPropertiesMapper.ApplyTo(cfg, merged);

            return cfg;
        });
    }

    public override IModuleProvider GetDefaultProvider() => new InMemoryCacheModuleProvider();


    public override IModuleProvider GetModuleProvider()
    {
        if (moduleProvider is not null)
            return moduleProvider;

        moduleProvider = ServiceProvider.GetService<ICacheModuleProvider>() ?? GetDefaultProvider();

        return moduleProvider;
    }

    public override ModulePipelineWrapper<TRequest, TResponse> GetModule<TRequest, TResponse>(string profileName)
    {
        var cacheIdentifier = base.GetModuleKey<TRequest, TResponse>(profileName);
        var cachedWrapper = GetWrapper<TRequest, TResponse>(cacheIdentifier);

        if (cachedWrapper is not null)
            return cachedWrapper;

        var provider = TryGetAttributeProvider(cacheIdentifier) ?? (ICacheModuleProvider)GetModuleProvider();

        if (provider is not null)
            CacheModulePipelineWrapper<TRequest, TResponse>.UseCustomCacheProvider(provider);

        var cacheConfig = (CacheModuleConfig)GetModuleConfig(cacheIdentifier);
        var wrapper = new CacheModulePipelineWrapper<TRequest, TResponse>(cacheConfig);

        CacheWrapper(cacheIdentifier, wrapper);

        return wrapper;
    }


    private ICacheModuleProvider TryGetAttributeProvider(ModuleIdentifier id)
    {
        var attributeConfig = ServiceProvider.GetKeyedService<ModuleConfig>(id);
        var providerName = attributeConfig?.GetModuleProperty("Provider") as string;
        if (string.IsNullOrEmpty(providerName))
            return null;

        var t = ResolveType(providerName);
        if (t == null)
            return null;

        if (ServiceProvider.GetService(t) is ICacheModuleProvider svc)
            return svc;

        try
        {
            var obj = ActivatorUtilities.CreateInstance(ServiceProvider, t);
            return obj as ICacheModuleProvider;
        }
        catch
        {
            return null;
        }
    }

    private static Type ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        var tn = TrimGlobal(typeName);
        var t = Type.GetType(tn, throwOnError: false);
        return t ?? null;
    }

    private static string TrimGlobal(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return typeName;
        const string prefix = "global::";

        return typeName.StartsWith(prefix, StringComparison.Ordinal) ? typeName[prefix.Length..] : typeName;
    }

    private IReadOnlyDictionary<string, CacheModuleOptions> GetGlobalProfiles()
    {
        return ServiceProvider.GetService<IModuleGlobalOptionsAccessor<CacheModuleOptions>>()?.Profiles
               ?? new Dictionary<string, CacheModuleOptions>();
    }

    private static Dictionary<string, object> ExtractProfileProperties(CacheModuleOptions profileOpt)
    {
        return profileOpt is null 
            ? [] 
            : CacheSettingsPropertiesMapper.ToDictionary(profileOpt);
    }

    private static string NormalizeProfileName(string name)
        => string.IsNullOrWhiteSpace(name) ? ModuleConstants.DefaultProfileName : name;
}