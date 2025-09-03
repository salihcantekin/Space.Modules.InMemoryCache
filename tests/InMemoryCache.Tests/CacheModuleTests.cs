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
        services.AddSpace();
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
}
