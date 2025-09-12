using System;
using System.Threading.Tasks;
using Space.Modules.InMemoryCache.Cache;
using Xunit;

namespace InMemoryCache.Tests;

public class InMemoryCacheModuleProviderTests
{
    private static CacheModuleConfig Cfg(TimeSpan ttl) => new() { TimeSpan = ttl };

    [Fact]
    public void GetKey_Returns_ToString_Of_Request()
    {
        var p = new InMemoryCacheModuleProvider();
        Assert.Equal("5", p.GetKey(5));
        Assert.Equal("abc", p.GetKey("abc"));
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse_And_DefaultOut()
    {
        var p = new InMemoryCacheModuleProvider();
        var ok = p.TryGet<string>("missing", out var value, Cfg(TimeSpan.FromSeconds(1)));
        Assert.False(ok);
        Assert.Null(value);
    }

    [Fact]
    public async Task Store_Then_TryGet_Before_Expiry_Returns_Cached()
    {
        var p = new InMemoryCacheModuleProvider();
        var key = "k1";
        await p.Store(key, "v1", Cfg(TimeSpan.FromMilliseconds(200)));

        var ok = p.TryGet<string>(key, out var value, Cfg(TimeSpan.FromMilliseconds(200)));
        Assert.True(ok);
        Assert.Equal("v1", value);
    }

    [Fact]
    public async Task Expired_Entry_Is_Not_Returned_After_TTL()
    {
        var p = new InMemoryCacheModuleProvider();
        var key = "k2";
        await p.Store(key, "v2", Cfg(TimeSpan.FromMilliseconds(50)));

        await Task.Delay(120);

        var ok = p.TryGet<string>(key, out var value, Cfg(TimeSpan.FromMilliseconds(50)));
        Assert.False(ok);
        Assert.Null(value);

        // re-store should work fine
        await p.Store(key, "v3", Cfg(TimeSpan.FromMilliseconds(200)));
        var ok2 = p.TryGet<string>(key, out var value2, Cfg(TimeSpan.FromMilliseconds(200)));
        Assert.True(ok2);
        Assert.Equal("v3", value2);
    }

    [Fact]
    public async Task Zero_TTL_Means_No_Expiration()
    {
        var p = new InMemoryCacheModuleProvider();
        var key = "k3";
        await p.Store(key, 42, Cfg(TimeSpan.Zero));

        await Task.Delay(150);

        var ok = p.TryGet<int>(key, out var value, Cfg(TimeSpan.Zero));
        Assert.True(ok);
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task Overwrite_Existing_Key_Replaces_Value()
    {
        var p = new InMemoryCacheModuleProvider();
        var key = "k4";
        await p.Store(key, "a", Cfg(TimeSpan.FromSeconds(1)));
        await p.Store(key, "b", Cfg(TimeSpan.FromSeconds(1)));

        var ok = p.TryGet<string>(key, out var value, Cfg(TimeSpan.FromSeconds(1)));
        Assert.True(ok);
        Assert.Equal("b", value);
    }
}
