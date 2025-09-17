using InMemoryCache.Tests.Providers;
using Microsoft.Extensions.DependencyInjection;
using Space.Abstraction;
using Space.Abstraction.Attributes;
using Space.Abstraction.Context;
using Space.Abstraction.Contracts;
using Space.DependencyInjection;
using Space.Modules.InMemoryCache;
using Space.Modules.InMemoryCache.Cache;

namespace InMemoryCache.Tests;

public class HandleTests
{
    public record HandleWithNameRequest(int Id) : IRequest<HandleWithNameResponse>;
    public record HandleWithNameResponse(Guid Id);

    public record IntReq(int Value) : IRequest<int>;

    public class TestHandler
    {
        public Func<HandlerContext<IntReq>, ValueTask<int>> HandleIntReqFunc;
        public Func<HandlerContext<HandleWithNameRequest>, ValueTask<HandleWithNameResponse>> HandleWithNameFunc;

        [Handle]
        public virtual ValueTask<int> Handle_IntReq(HandlerContext<IntReq> ctx)
            => HandleIntReqFunc != null ? HandleIntReqFunc(ctx) : ValueTask.FromResult(0);

        [Handle(Name = "This_is_handle_name")]
        public virtual ValueTask<HandleWithNameResponse> Handle_WithName(HandlerContext<HandleWithNameRequest> ctx)
            => HandleWithNameFunc != null ? HandleWithNameFunc(ctx) : ValueTask.FromResult(new HandleWithNameResponse(Guid.Empty));
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSpace(opt => opt.ServiceLifetime = ServiceLifetime.Singleton);
        services.AddSpaceInMemoryCache<TestCustomCacheProvider>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Send_WithRequestResponse_ShouldReturnExpected()
    {
        using var sp = BuildProvider();
        var space = sp.GetRequiredService<ISpace>();
        var handler = sp.GetRequiredService<TestHandler>();

        handler.HandleIntReqFunc = ctx =>
        {
            Assert.NotNull(ctx);
            Assert.Equal(5, ctx.Request.Value);
            return ValueTask.FromResult(10);
        };

        var res = await space.Send<IntReq, int>(new IntReq(5));
        Assert.Equal(10, res);
    }

    [Fact]
    public async Task SendReqRes_WithName_ShouldReturnExpected()
    {
        using var sp = BuildProvider();
        var space = sp.GetRequiredService<ISpace>();
        var handler = sp.GetRequiredService<TestHandler>();

        handler.HandleWithNameFunc = ctx => ValueTask.FromResult(new HandleWithNameResponse(Guid.NewGuid()));

        var res = await space.Send<HandleWithNameRequest, HandleWithNameResponse>(new HandleWithNameRequest(42), "This_is_handle_name");

        Assert.NotNull(res);
        Assert.IsType<HandleWithNameResponse>(res);
        Assert.NotEqual(Guid.Empty, res.Id);
    }

    // Manual eviction scenario tests using ICacheModuleProvider

    public record QueryA(int Id) : IRequest<string>;
    public record UpdateA(int Id, string Value) : IRequest<Nothing>;

    public class QueryHandler
    {
        [Handle]
        [CacheModule(Duration = 60)]
        public virtual ValueTask<string> Get(HandlerContext<QueryA> ctx)
            => ValueTask.FromResult($"Q:{ctx.Request.Id}:{Guid.NewGuid()}");
    }

    public class UpdateHandler(ICacheModuleProvider cache)
    {
        [Handle]
        public virtual ValueTask<Nothing> Update(HandlerContext<UpdateA> ctx)
        {
            var key = cache.GetKey(new QueryA(ctx.Request.Id));
            cache.Remove(key);
            return Nothing.ValueTask;
        }
    }

    [Fact]
    public async Task Manual_Remove_From_Command_Through_Provider_Empties_Cached_Query()
    {
        using var sp = BuildProvider();
        var space = sp.GetRequiredService<ISpace>();

        // warm cache
        var r1 = await space.Send<QueryA, string>(new QueryA(1));
        var r2 = await space.Send<QueryA, string>(new QueryA(1));
        Assert.Equal(r1, r2);

        // invalidate via command
        await space.Send<UpdateA, Nothing>(new UpdateA(1, "v"));

        // next query should miss and return different value
        var r3 = await space.Send<QueryA, string>(new QueryA(1));
        Assert.NotEqual(r2, r3);
    }
}
