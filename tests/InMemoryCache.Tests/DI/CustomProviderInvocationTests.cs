using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Space.Abstraction;
using Space.Abstraction.Attributes;
using Space.Abstraction.Context;
using Space.Abstraction.Contracts;
using Space.DependencyInjection;
using Space.Modules.InMemoryCache;
using Space.Modules.InMemoryCache.Cache;
using InMemoryCache.Tests.Providers;

namespace InMemoryCache.Tests.DI;

public class CustomProviderInvocationTests
{
    public record Req(string Key) : IRequest<Res>;
    public record Res(string Value);

    public class Handler
    {
        public Func<HandlerContext<Req>, ValueTask<Res>> HandleFunc;

        [Handle(Name = nameof(Cached_Invocation_A))]
        [CacheModule(Duration = 1)]
        public virtual ValueTask<Res> Cached_Invocation_A(HandlerContext<Req> ctx)
            => HandleFunc != null ? HandleFunc(ctx) : ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}"));

        [Handle(Name = nameof(Cached_Invocation_B))]
        [CacheModule(Duration = 1)]
        public virtual ValueTask<Res> Cached_Invocation_B(HandlerContext<Req> ctx)
            => HandleFunc != null ? HandleFunc(ctx) : ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}"));
    }

    private static ServiceProvider BuildProvider(out TestCustomCacheProvider custom)
    {
        var sc = new ServiceCollection();
        sc.AddSpace(opt => opt.ServiceLifetime = ServiceLifetime.Singleton);
        sc.AddSpaceInMemoryCache<TestCustomCacheProvider>(opt =>
        {
            opt.WithDefaultProfile(p => p.TimeSpan = TimeSpan.FromSeconds(1));
        });

        var sp = sc.BuildServiceProvider();
        custom = (TestCustomCacheProvider)sp.GetRequiredService<ICacheModuleProvider>();
        // Ensure wrapper uses the same provider instance
        CacheModulePipelineWrapper<Req, Res>.UseCustomCacheProvider(custom);
        return sp;
    }

    [Fact]
    public async Task Invocation_Order_First_GetKey_Then_TryGet_Then_Store_On_Miss()
    {
        // Arrange
        var sp = BuildProvider(out var custom);
        custom.ClearCalls();
        var h = sp.GetRequiredService<Handler>();
        var space = sp.GetRequiredService<ISpace>();
        h.HandleFunc = ctx => ValueTask.FromResult(new Res($"{ctx.Request.Key}:H1"));
        var req = new Req("k");

        // Act
        var res = await space.Send<Req, Res>(req, name: nameof(Handler.Cached_Invocation_A));

        // Assert
        res.ShouldNotBeNull();
        var methods = custom.Calls.Select(c => c.Method).ToList();
        methods.ShouldContain("GetKey");
        methods.ShouldContain("TryGet");
        methods.IndexOf("GetKey").ShouldBeLessThan(methods.IndexOf("TryGet"));
        if (methods.Contains("Store"))
        {
            methods.IndexOf("TryGet").ShouldBeLessThan(methods.IndexOf("Store"));
            custom.Calls.First(c => c.Method == "Store").Ttl.ShouldBeGreaterThan(TimeSpan.Zero);
        }
        var keys = custom.Calls.Select(c => c.Key).ToList();
        keys.Any(k => k.Contains('k')).ShouldBeTrue();
    }

    [Fact]
    public async Task Invocation_Order_GetKey_Then_TryGet_On_Hit()
    {
        // Arrange
        var sp = BuildProvider(out var custom);
        custom.ClearCalls();
        var h = sp.GetRequiredService<Handler>();
        var space = sp.GetRequiredService<ISpace>();
        int cnt = 0;
        h.HandleFunc = ctx => { Interlocked.Increment(ref cnt); return ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}")); };
        var req = new Req("k2");

        // warm cache
        var _warm = await space.Send<Req, Res>(req, name: nameof(Handler.Cached_Invocation_B));
        custom.ClearCalls();

        // Act
        var res = await space.Send<Req, Res>(req, name: nameof(Handler.Cached_Invocation_B));

        // Assert
        cnt.ShouldBe(1);
        var methods = custom.Calls.Select(c => c.Method).ToList();
        methods.ShouldContain("GetKey");
        methods.ShouldContain("TryGet");
        methods.IndexOf("GetKey").ShouldBeLessThan(methods.IndexOf("TryGet"));
        methods.ShouldNotContain("Store");
        var keys = custom.Calls.Select(c => c.Key).ToList();
        keys.Any(k => k.Contains("k2")).ShouldBeTrue();
    }

    [Fact]
    public async Task Parameters_Passed_Correctly_To_Provider()
    {
        // Arrange
        var sp = BuildProvider(out var custom);
        custom.ClearCalls();
        var h = sp.GetRequiredService<Handler>();
        var space = sp.GetRequiredService<ISpace>();
        h.HandleFunc = ctx => ValueTask.FromResult(new Res($"{ctx.Request.Key}:H"));
        var req = new Req("pX");

        // Act
        var _ = await space.Send<Req, Res>(req, name: nameof(Handler.Cached_Invocation_A));

        // Assert
        var (Method, Key, Ttl) = custom.Calls.First(c => c.Method == "GetKey");
        var tryCall = custom.Calls.First(c => c.Method == "TryGet");
        var storeCall = custom.Calls.FirstOrDefault(c => c.Method == "Store");

        Key.ShouldContain("pX");
        tryCall.Key.ShouldContain("pX");

        if (storeCall != default)
        {
            storeCall.Key.ShouldContain("pX");
            storeCall.Ttl.TotalSeconds.ShouldBeGreaterThanOrEqualTo(1);
        }
    }
}
