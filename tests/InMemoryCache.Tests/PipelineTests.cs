using InMemoryCache.Tests.Providers;
using Microsoft.Extensions.DependencyInjection;
using Space.Abstraction;
using Space.Abstraction.Attributes;
using Space.Abstraction.Context;
using Space.Abstraction.Contracts;
using Space.DependencyInjection;
using Space.Modules.InMemoryCache;

namespace InMemoryCache.Tests;

public class PipelineTests
{
    public record Req(string Text): IRequest<Res>;
    public record Res(string Text);

    public class PipelineHandler
    {
        public Func<HandlerContext<Req>, ValueTask<Res>>? HandleFunc;
        public Func<PipelineContext<Req>, PipelineDelegate<Req, Res>, ValueTask<Res>>? P2Func;
        public Func<PipelineContext<Req>, PipelineDelegate<Req, Res>, ValueTask<Res>>? P1Func;

        [Handle(Name = "hn")]
        public virtual ValueTask<Res> Handle(HandlerContext<Req> ctx)
            => HandleFunc != null ? HandleFunc(ctx) : ValueTask.FromResult(new Res(ctx.Request.Text + ":H"));

        [Pipeline("hn", Order = 2)]
        public virtual ValueTask<Res> P2(PipelineContext<Req> ctx, PipelineDelegate<Req, Res> next)
            => P2Func != null ? P2Func(ctx, next) : next(ctx);

        [Pipeline("hn", Order = 1)]
        public virtual ValueTask<Res> P1(PipelineContext<Req> ctx, PipelineDelegate<Req, Res> next)
            => P1Func != null ? P1Func(ctx, next) : next(ctx);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSpace(opt => opt.ServiceLifetime = ServiceLifetime.Singleton);
        services.AddSpaceInMemoryCache<TestCustomCacheProvider>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Pipeline_Order_Is_Applied_And_Next_Called()
    {
        using var sp = BuildProvider();
        var space = sp.GetRequiredService<ISpace>();
        var handler = sp.GetRequiredService<PipelineHandler>();

        handler.HandleFunc = ctx => ValueTask.FromResult(new Res(ctx.Request.Text + ":H"));
        handler.P2Func = async (ctx, next) =>
        {
            var res = await next(ctx);
            return res with { Text = res.Text + ":P2" };
        };
        handler.P1Func = async (ctx, next) =>
        {
            var res = await next(ctx);
            return res with { Text = res.Text + ":P1" };
        };

        var req = new Req("X");
        var res = await space.Send<Req, Res>(req, name: "hn");

        Assert.Equal("X:H:P2:P1", res.Text);
    }
}
