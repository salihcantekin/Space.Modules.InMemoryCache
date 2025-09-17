using Microsoft.Extensions.DependencyInjection;
using Space.Abstraction;
using Space.Abstraction.Attributes;
using Space.Abstraction.Context;
using Space.Abstraction.Contracts;
using Space.DependencyInjection;
using Space.Modules.InMemoryCache;
using Space.Modules.InMemoryCache.Cache;

namespace InMemoryCache.Tests;

public class CacheModuleTests
{
    public record CreateUser(string Email) : IRequest<UserDto>;
    public record UserDto(string Id);

    public class CreateUserHandler
    {
        public Func<HandlerContext<CreateUser>, ValueTask<UserDto>>? HandleFunc;

        [Handle]
        [CacheModule(Duration = 60)]
        public virtual ValueTask<UserDto> Handle(HandlerContext<CreateUser> ctx)
            => HandleFunc != null ? HandleFunc(ctx) : ValueTask.FromResult(new UserDto($"{ctx.Request.Email}:{Guid.NewGuid()}"));
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSpace(opt => opt.ServiceLifetime = ServiceLifetime.Singleton);
        services.AddSpaceInMemoryCache();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task CacheModule_Returns_Cached_Response()
    {
        using var sp = BuildProvider();
        var handler = sp.GetRequiredService<CreateUserHandler>();
        var space = sp.GetRequiredService<ISpace>();

        int handleCount = 0;
        handler.HandleFunc = ctx =>
        {
            handleCount++;
            return ValueTask.FromResult(new UserDto($"{ctx.Request.Email}:{Guid.NewGuid()}"));
        };

        var req = new CreateUser("a@b.com");

        var r1 = await space.Send<CreateUser, UserDto>(req);
        var r2 = await space.Send<CreateUser, UserDto>(req);

        Assert.Equal(1, handleCount); // second call should use cache
        Assert.Equal(r1.Id, r2.Id);
    }

    public record GetUser(string Email) : IRequest<UserDto>;

    public class QueryHandler
    {
        public Func<HandlerContext<GetUser>, ValueTask<UserDto>>? GetHandle;

        [Handle]
        [CacheModule(Duration = 60)]
        public virtual ValueTask<UserDto> Get(HandlerContext<GetUser> ctx)
            => GetHandle != null ? GetHandle(ctx) : ValueTask.FromResult(new UserDto($"{ctx.Request.Email}:{Guid.NewGuid()}"));
    }

    public record UpdateUser(string Email, string NewId) : IRequest<UserDto>;

    public class CommandHandler
    {
        private readonly ICacheModuleProvider cache;
        public CommandHandler(ICacheModuleProvider cache) => this.cache = cache;

        [Handle]
        public virtual ValueTask<UserDto> Update(HandlerContext<UpdateUser> ctx)
        {
            // mutasyon sonras? ilgili query cache’ini invalid et
            var key = cache.GetKey(new GetUser(ctx.Request.Email));
            cache.Remove(key);
            return ValueTask.FromResult(new UserDto(ctx.Request.NewId));
        }
    }

    private static ServiceProvider BuildProviderWithEviction()
    {
        var services = new ServiceCollection();
        services.AddSpace(opt => opt.ServiceLifetime = ServiceLifetime.Singleton);
        services.AddSpaceInMemoryCache();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Manual_Eviction_From_Command_Invalidate_Cached_Query()
    {
        using var sp = BuildProviderWithEviction();
        var space = sp.GetRequiredService<ISpace>();
        var qh = sp.GetRequiredService<QueryHandler>();

        // 1) ?lk sorgu -> cache’e yazar
        var r1 = await space.Send<GetUser, UserDto>(new GetUser("x@x.com"));
        // 2) Ayn? sorgu -> cache’den okur
        var r2 = await space.Send<GetUser, UserDto>(new GetUser("x@x.com"));
        Assert.Equal(r1.Id, r2.Id);

        // 3) Mutasyon -> ilgili query key’i Remove edilir
        var updated = await space.Send<UpdateUser, UserDto>(new UpdateUser("x@x.com", "forced"));
        Assert.Equal("forced", updated.Id);

        // 4) Tekrar sorgu -> cache invalid oldu?u için yeni de?er üretir
        qh.GetHandle = ctx => ValueTask.FromResult(new UserDto("after"));
        var r3 = await space.Send<GetUser, UserDto>(new GetUser("x@x.com"));
        Assert.NotEqual(r2.Id, r3.Id);
        Assert.Equal("after", r3.Id);
    }
}
