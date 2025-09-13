using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Space.Abstraction;
using Space.Abstraction.Attributes;
using Space.Abstraction.Context;
using Space.Abstraction.Contracts;
using Space.DependencyInjection;
using Space.Modules.InMemoryCache;
using Space.Modules.InMemoryCache.Cache;

namespace InMemoryCache.Tests.DI;

public class CustomProviderMoqTests
{
    public record Req(string Key) : IRequest<Res>;
    public record Res(string Value);

    public class Handler
    {
        public Func<HandlerContext<Req>, ValueTask<Res>> HandleFunc;

        [Handle(Name = nameof(Moq_A))]
        [CacheModule(Duration = 1)]
        public virtual ValueTask<Res> Moq_A(HandlerContext<Req> ctx)
            => HandleFunc != null ? HandleFunc(ctx) : ValueTask.FromResult(new Res($"{ctx.Request.Key}:H1"));

        [Handle(Name = nameof(Moq_B))]
        [CacheModule(Duration = 1)]
        public virtual ValueTask<Res> Moq_B(HandlerContext<Req> ctx)
            => HandleFunc != null ? HandleFunc(ctx) : ValueTask.FromResult(new Res($"{ctx.Request.Key}:H2"));
    }

    private static ServiceProvider BuildProvider(Mock<ICacheModuleProvider> mock)
    {
        var sc = new ServiceCollection();
        sc.AddSpace(opt => opt.ServiceLifetime = ServiceLifetime.Singleton);
        sc.AddSpaceInMemoryCache(opt => opt.WithDefaultProfile(p => p.TimeSpan = TimeSpan.FromSeconds(1)));
        // Override default registration with our mock
        sc.AddSingleton<ICacheModuleProvider>(sp => mock.Object);
        var sp = sc.BuildServiceProvider();
        // Also ensure wrapper uses the mock explicitly for these types
        CacheModulePipelineWrapper<Req, Res>.UseCustomCacheProvider(mock.Object);
        return sp;
    }

    [Fact]
    public async Task Miss_Then_Store_Order_And_Params()
    {
        // Arrange
        var mock = new Mock<ICacheModuleProvider>(MockBehavior.Strict);
        var key = "k";
        Res stored = null;
        var seq = new MockSequence();

        mock.InSequence(seq)
            .Setup(m => m.GetKey(It.IsAny<Req>()))
            .Returns(key);

        Res outMiss = null!;
        mock.InSequence(seq)
            .Setup(m => m.TryGet(key, out outMiss, It.IsAny<CacheModuleConfig>()))
            .Returns(false);

        mock.InSequence(seq)
            .Setup(m => m.Store(key, It.IsAny<Res>(), It.Is<CacheModuleConfig>(c => c.TimeSpan >= TimeSpan.FromSeconds(1))))
            .Callback<string, Res, CacheModuleConfig>((k, r, c) => stored = r)
            .Returns(ValueTask.CompletedTask);

        using var sp = BuildProvider(mock);
        var space = sp.GetRequiredService<ISpace>();
        var handler = sp.GetRequiredService<Handler>();
        handler.HandleFunc = ctx => ValueTask.FromResult(new Res($"{ctx.Request.Key}:H1"));

        // Act
        var res = await space.Send<Req, Res>(new Req("x"), name: nameof(Handler.Moq_A));

        // Assert
        res.ShouldNotBeNull();
        mock.Verify(m => m.GetKey(It.IsAny<Req>()), Times.Once);
        // verify one TryGet regardless of out value
        Res any = null!;
        mock.Verify(m => m.TryGet(key, out any, It.IsAny<CacheModuleConfig>()), Times.Once);
        mock.Verify(m => m.Store(key, It.IsAny<Res>(), It.IsAny<CacheModuleConfig>()), Times.Once);
    }

    [Fact]
    public async Task Hit_On_Second_Call_No_Store_On_Hit()
    {
        // Arrange
        var mock = new Mock<ICacheModuleProvider>(MockBehavior.Strict);
        var key = "k2";
        Res saved = null;
        var seq = new MockSequence();

        mock.InSequence(seq)
            .Setup(m => m.GetKey(It.IsAny<Req>()))
            .Returns(key);

        Res outMiss = null!;
        mock.InSequence(seq)
            .Setup(m => m.TryGet(key, out outMiss, It.IsAny<CacheModuleConfig>()))
            .Returns(false);

        mock.InSequence(seq)
            .Setup(m => m.Store(key, It.IsAny<Res>(), It.IsAny<CacheModuleConfig>()))
            .Callback<string, Res, CacheModuleConfig>((k, r, c) => saved = r)
            .Returns(ValueTask.CompletedTask);

        // Second call
        mock.InSequence(seq)
            .Setup(m => m.GetKey(It.IsAny<Req>()))
            .Returns(key);

        mock.InSequence(seq)
            .Setup(m => m.TryGet(key, out saved!, It.IsAny<CacheModuleConfig>()))
            .Returns(true);

        using var sp = BuildProvider(mock);
        var space = sp.GetRequiredService<ISpace>();
        var handler = sp.GetRequiredService<Handler>();
        int cnt = 0;
        handler.HandleFunc = ctx => { cnt++; return ValueTask.FromResult(new Res($"{ctx.Request.Key}:H2")); };

        // Act
        var r1 = await space.Send<Req, Res>(new Req("x2"), name: nameof(Handler.Moq_B));
        var r2 = await space.Send<Req, Res>(new Req("x2"), name: nameof(Handler.Moq_B));

        // Assert
        cnt.ShouldBe(1);
        mock.Verify(m => m.GetKey(It.IsAny<Req>()), Times.Exactly(2));
        mock.Verify(m => m.Store(key, It.IsAny<Res>(), It.IsAny<CacheModuleConfig>()), Times.Once);
        Res any = null!;
        mock.Verify(m => m.TryGet(key, out any, It.IsAny<CacheModuleConfig>()), Times.Exactly(2));
    }
}
