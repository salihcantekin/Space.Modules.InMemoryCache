using Microsoft.Extensions.DependencyInjection;
using Space.Abstraction;
using Space.Abstraction.Attributes;
using Space.Abstraction.Context;
using Space.DependencyInjection;

namespace InMemoryCache.Tests;

public class NotificationTests
{
    public record Ping(int Id);

    public class NotificationHandlers
    {
        public Func<NotificationContext<int>, ValueTask>? OnIntFunc;
        public Func<NotificationContext<int>, ValueTask>? OnIntNamedFunc;
        public Func<NotificationContext<Ping>, ValueTask>? OnPingAFunc;
        public Func<NotificationContext<Ping>, ValueTask>? OnPingBFunc;

        [Notification]
        public virtual ValueTask OnInt(NotificationContext<int> ctx)
            => OnIntFunc != null ? OnIntFunc(ctx) : ValueTask.CompletedTask;

        [Notification(HandleName = "named")]
        public virtual ValueTask OnIntNamed(NotificationContext<int> ctx)
            => OnIntNamedFunc != null ? OnIntNamedFunc(ctx) : ValueTask.CompletedTask;

        [Notification(HandleName = "A")]
        public virtual ValueTask OnPingA(NotificationContext<Ping> ctx)
            => OnPingAFunc != null ? OnPingAFunc(ctx) : ValueTask.CompletedTask;

        [Notification(HandleName = "B")]
        public virtual ValueTask OnPingB(NotificationContext<Ping> ctx)
            => OnPingBFunc != null ? OnPingBFunc(ctx) : ValueTask.CompletedTask;
    }

    private static ServiceProvider BuildProvider(Action<SpaceOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSpace(opt =>
        {
            if (configure != null)
                configure(opt);
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Publish_ToAllSubscribers_ByType()
    {
        using var sp = BuildProvider();
        var space = sp.GetRequiredService<ISpace>();
        var handlers = sp.GetRequiredService<NotificationHandlers>();

        bool called = false;
        NotificationContext<int>? receivedCtx = null;
        handlers.OnIntFunc = ctx =>
        {
            called = true;
            receivedCtx = ctx;
            return ValueTask.CompletedTask;
        };

        await space.Publish(5);

        Assert.True(called);
        Assert.NotNull(receivedCtx);
        Assert.Equal(5, receivedCtx.Request);
    }

    [Fact]
    public async Task Publish_WithName_TargetsOnlyMatching()
    {
        using var sp = BuildProvider();
        var space = sp.GetRequiredService<ISpace>();
        var handlers = sp.GetRequiredService<NotificationHandlers>();

        bool namedCalled = false;
        NotificationContext<int>? receivedCtx = null;
        handlers.OnIntNamedFunc = ctx =>
        {
            namedCalled = true;
            receivedCtx = ctx;
            return ValueTask.CompletedTask;
        };

        await space.Publish(5, name: "named");

        Assert.True(namedCalled);
        Assert.NotNull(receivedCtx);
        Assert.Equal(5, receivedCtx.Request);
    }

    [Fact]
    public async Task Publish_Parallel_Dispatches_All_Type_Subscribers()
    {
        using var sp = BuildProvider(opt => opt.NotificationDispatchType = NotificationDispatchType.Parallel);
        var space = sp.GetRequiredService<ISpace>();
        var handlers = sp.GetRequiredService<NotificationHandlers>();

        bool pingACalled = false, pingBCalled = false;
        NotificationContext<Ping>? ctxA = null, ctxB = null;

        handlers.OnPingAFunc = ctx => { pingACalled = true; ctxA = ctx; return ValueTask.CompletedTask; };
        handlers.OnPingBFunc = ctx => { pingBCalled = true; ctxB = ctx; return ValueTask.CompletedTask; };

        await space.Publish(new Ping(1));

        Assert.True(pingACalled);
        Assert.True(pingBCalled);
        Assert.NotNull(ctxA);
        Assert.NotNull(ctxB);
        Assert.Equal(1, ctxA!.Request.Id);
        Assert.Equal(1, ctxB!.Request.Id);
    }
}
