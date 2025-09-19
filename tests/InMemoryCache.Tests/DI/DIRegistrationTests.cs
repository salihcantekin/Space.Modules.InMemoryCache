using Microsoft.Extensions.DependencyInjection;
using Space.Abstraction;
using Space.Abstraction.Attributes;
using Space.Abstraction.Context;
using Space.Abstraction.Contracts;
using Space.DependencyInjection;
using Space.Modules.InMemoryCache;
using Space.Modules.InMemoryCache.Cache;
using InMemoryCache.Tests.Providers;

namespace InMemoryCache.Tests.DI;

public class DIRegistrationTests
{
    public record Req(string Key) : IRequest<Res>;
    public record Res(string Value);

    public class Handler
    {
        public Func<HandlerContext<Req>, ValueTask<Res>> HandleFunc;

        [Handle(Name = nameof(Cached))]
        [CacheModule(Duration = 1)]
        public virtual ValueTask<Res> Cached(HandlerContext<Req> ctx)
            => HandleFunc != null ? HandleFunc(ctx) : ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}"));
    }

    [Fact]
    public void AddSpaceInMemoryCache_Default_Registers_DefaultProvider()
    {
        var sc = new ServiceCollection();
        sc.AddSpace(opt => opt.ServiceLifetime = ServiceLifetime.Singleton);
        sc.AddSpaceInMemoryCache();

        using var sp = sc.BuildServiceProvider();
        var provider = sp.GetRequiredService<ICacheModuleProvider>();
        Assert.IsType<InMemoryCacheModuleProvider>(provider);
    }

    [Fact]
    public async Task AddSpaceInMemoryCache_WithCustomProvider_Registers_And_Uses_It()
    {
        var sc = new ServiceCollection();
        sc.AddSpace(opt => opt.ServiceLifetime = ServiceLifetime.Singleton);
        sc.AddSpaceInMemoryCache<TestCustomCacheProvider>(opt =>
        {
            opt.WithDefaultProfile(p => p.TimeSpan = TimeSpan.FromSeconds(1));
        });

        using var sp = sc.BuildServiceProvider();
        var provider = sp.GetRequiredService<ICacheModuleProvider>();
        Assert.IsType<TestCustomCacheProvider>(provider);

        var handler = sp.GetRequiredService<Handler>();
        var space = sp.GetRequiredService<ISpace>();

        int cnt = 0;
        handler.HandleFunc = ctx =>
        {
            Interlocked.Increment(ref cnt);
            return ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}"));
        };

        var req = new Req("x");
        var r1 = await space.Send<Req, Res>(req, name: nameof(Handler.Cached));
        var r2 = await space.Send<Req, Res>(req, name: nameof(Handler.Cached));

        var custom = (TestCustomCacheProvider)provider;
        Assert.Equal(1, cnt); // second should be served from custom provider
        Assert.False(string.IsNullOrEmpty(custom.LastStoredKey));
        Assert.False(string.IsNullOrEmpty(custom.LastTryGetKey));
        Assert.Contains("x", custom.LastStoredKey);
        Assert.Contains("x", custom.LastTryGetKey);
    }
}
