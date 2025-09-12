using Microsoft.Extensions.DependencyInjection;
using Space.Abstraction;
using Space.Abstraction.Attributes;
using Space.Abstraction.Context;
using Space.Abstraction.Contracts;
using Space.DependencyInjection;
using Space.Modules.InMemoryCache;
using Space.Modules.InMemoryCache.Cache;

namespace InMemoryCache.Tests;

public class CacheProfileOptionsTests
{
    public record Req(string Key) : IRequest<Res>;
    public record Res(string Value);

    public class Handler
    {
        public Func<HandlerContext<Req>, ValueTask<Res>>? HandleFunc;

        [Handle(Name = nameof(DefaultProfile))]
        [CacheModule] // no properties set -> should use global Default profile
        public virtual ValueTask<Res> DefaultProfile(HandlerContext<Req> ctx)
            => HandleFunc != null ? HandleFunc(ctx) : ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}"));

        [Handle(Name = nameof(NamedProfile))]
        [CacheModule(Profile = "fast")] // named profile
        public virtual ValueTask<Res> NamedProfile(HandlerContext<Req> ctx)
            => HandleFunc != null ? HandleFunc(ctx) : ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}"));

        [Handle(Name = nameof(AttributeOverrides))]
        [CacheModule(Duration = 1, Profile = "slow")] // attribute sets Duration => overrides profile values
        public virtual ValueTask<Res> AttributeOverrides(HandlerContext<Req> ctx)
            => HandleFunc != null ? HandleFunc(ctx) : ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}"));
    }

    private static ServiceProvider BuildProvider(Action<CacheModuleOptions> cacheOpt)
    {
        var services = new ServiceCollection();
        services.AddSpace(opt => opt.ServiceLifetime = ServiceLifetime.Singleton);
        services.AddSpaceInMemoryCache(cacheOpt);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task DefaultProfile_Applies_When_Attribute_Has_No_Properties()
    {
        using var sp = BuildProvider(opt =>
        {
            opt.WithDefaultProfile(p => p.TimeSpan = TimeSpan.FromMilliseconds(50));
        });

        var h = sp.GetRequiredService<Handler>();
        var space = sp.GetRequiredService<ISpace>();

        int cnt = 0;
        h.HandleFunc = ctx =>
        {
            Interlocked.Increment(ref cnt);
            return ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}"));
        };

        var req = new Req("a");

        var r1 = await space.Send<Req, Res>(req, name: nameof(Handler.DefaultProfile));
        var r2 = await space.Send<Req, Res>(req, name: nameof(Handler.DefaultProfile));
        Assert.Equal(1, cnt); // second call should be cached (within TTL)
        Assert.Equal(r1.Value, r2.Value);

        await Task.Delay(100);
        var r3 = await space.Send<Req, Res>(req, name: nameof(Handler.DefaultProfile));
        Assert.Equal(2, cnt); // expired due to default profile
        Assert.NotEqual(r2.Value, r3.Value);
    }

    [Fact]
    public async Task Named_Profile_Applies_When_Specified_On_Attribute()
    {
        using var sp = BuildProvider(opt =>
        {
            opt.WithProfile("fast", p => p.TimeSpan = TimeSpan.FromMilliseconds(50));
            opt.WithDefaultProfile(p => p.TimeSpan = TimeSpan.FromSeconds(60));
        });

        var h = sp.GetRequiredService<Handler>();
        var space = sp.GetRequiredService<ISpace>();

        int cnt = 0;
        h.HandleFunc = ctx =>
        {
            Interlocked.Increment(ref cnt);
            return ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}"));
        };

        var req = new Req("b");

        var r1 = await space.Send<Req, Res>(req, name: nameof(Handler.NamedProfile));
        var r2 = await space.Send<Req, Res>(req, name: nameof(Handler.NamedProfile));
        Assert.Equal(1, cnt); // within fast profile TTL
        Assert.Equal(r1.Value, r2.Value);

        await Task.Delay(100);
        var r3 = await space.Send<Req, Res>(req, name: nameof(Handler.NamedProfile));
        Assert.Equal(2, cnt); // expired due to fast profile
        Assert.NotEqual(r2.Value, r3.Value);
    }

    [Fact]
    public async Task Attribute_Properties_Override_Profile_Values()
    {
        using var sp = BuildProvider(opt =>
        {
            // Profile defines very short ttl, but attribute sets Duration=1s which should override profile value
            opt.WithProfile("slow", p => p.TimeSpan = TimeSpan.FromMilliseconds(50));
            opt.WithDefaultProfile(p => p.TimeSpan = TimeSpan.FromMilliseconds(50));
        });

        var h = sp.GetRequiredService<Handler>();
        var space = sp.GetRequiredService<ISpace>();

        int cnt = 0;
        h.HandleFunc = ctx =>
        {
            Interlocked.Increment(ref cnt);
            return ValueTask.FromResult(new Res($"{ctx.Request.Key}:{Guid.NewGuid()}"));
        };

        var req = new Req("c");

        var r1 = await space.Send<Req, Res>(req, name: nameof(Handler.AttributeOverrides));
        await Task.Delay(200); // less than attribute Duration (1s)
        var r2 = await space.Send<Req, Res>(req, name: nameof(Handler.AttributeOverrides));

        Assert.Equal(1, cnt); // should not expire because attribute overrides profile (1s)
        Assert.Equal(r1.Value, r2.Value);

        await Task.Delay(1000);
        var r3 = await space.Send<Req, Res>(req, name: nameof(Handler.AttributeOverrides));
        Assert.Equal(2, cnt);
        Assert.NotEqual(r2.Value, r3.Value);
    }
}
