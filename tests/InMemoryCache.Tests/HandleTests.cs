using Microsoft.Extensions.DependencyInjection;
using Space.Abstraction;
using Space.Abstraction.Attributes;
using Space.Abstraction.Context;
using Space.Abstraction.Contracts;
using Space.DependencyInjection;

namespace InMemoryCache.Tests;

public class HandleTests
{
    public record HandleWithNameRequest(int Id) : IRequest<HandleWithNameResponse>;
    public record HandleWithNameResponse(Guid Id);

    public class TestHandler
    {
        public Func<HandlerContext<int>, ValueTask<int>>? HandleIntFunc;
        public Func<HandlerContext<HandleWithNameRequest>, ValueTask<HandleWithNameResponse>>? HandleWithNameFunc;

        [Handle]
        public virtual ValueTask<int> Handle_int_int(HandlerContext<int> ctx)
            => HandleIntFunc != null ? HandleIntFunc(ctx) : ValueTask.FromResult(0);

        [Handle(Name = "This_is_handle_name")]
        public virtual ValueTask<HandleWithNameResponse> Handle_WithName(HandlerContext<HandleWithNameRequest> ctx)
            => HandleWithNameFunc != null ? HandleWithNameFunc(ctx) : ValueTask.FromResult(new HandleWithNameResponse(Guid.Empty));
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSpace(opt => opt.ServiceLifetime = ServiceLifetime.Singleton);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Send_WithRequestResponse_ShouldReturnExpected()
    {
        using var sp = BuildProvider();
        var space = sp.GetRequiredService<ISpace>();
        var handler = sp.GetRequiredService<TestHandler>();

        handler.HandleIntFunc = ctx =>
        {
            Assert.NotNull(ctx);
            Assert.Equal(5, ctx.Request);
            return ValueTask.FromResult(10);
        };

        var res = await space.Send<int, int>(5);
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
}
