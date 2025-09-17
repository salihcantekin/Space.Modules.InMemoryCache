using System;
using System.Threading.Tasks;
using Space.Modules.InMemoryCache.Cache;
using Xunit;

namespace InMemoryCache.Tests;

public class InMemoryCacheModuleProviderTests
{
    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => start;
        public void Advance(TimeSpan by) => start = start.Add(by);
    }

    private static CacheModuleConfig Cfg(TimeSpan ttl) => new() { TimeSpan = ttl };

    [Fact]
    public void GetKey_Returns_ToString_Of_Request()
    {
        var p = new InMemoryCacheModuleProvider(TimeProvider.System);
        Assert.Equal("5", p.GetKey(5));
        Assert.Equal("abc", p.GetKey("abc"));
    }

    private sealed class CustomReq
    {
        public string X { get; set; } = string.Empty;
        public override string ToString() => $"custom:{X.ToLowerInvariant()}";
    }

    [Fact]
    public void GetKey_Uses_Request_ToString_Override()
    {
        var p = new InMemoryCacheModuleProvider(TimeProvider.System);
        var key = p.GetKey(new CustomReq { X = "ABC" });
        Assert.Equal("custom:abc", key);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse_And_DefaultOut()
    {
        var p = new InMemoryCacheModuleProvider(TimeProvider.System);
        var ok = p.TryGet<string>("missing", out var value, Cfg(TimeSpan.FromSeconds(1)));
        Assert.False(ok);
        Assert.Null(value);
    }

    [Fact]
    public async Task Store_Then_TryGet_Before_Expiry_Returns_Cached()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var p = new InMemoryCacheModuleProvider(clock);
        var key = "k1";
        await p.Store(key, "v1", Cfg(TimeSpan.FromMilliseconds(200)));

        var ok = p.TryGet<string>(key, out var value, Cfg(TimeSpan.FromMilliseconds(200)));
        Assert.True(ok);
        Assert.Equal("v1", value);
    }

    [Fact]
    public async Task Expired_Entry_Is_Not_Returned_After_TTL()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var p = new InMemoryCacheModuleProvider(clock);
        var key = "k2";
        await p.Store(key, "v2", Cfg(TimeSpan.FromMilliseconds(50)));

        clock.Advance(TimeSpan.FromMilliseconds(120));

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
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var p = new InMemoryCacheModuleProvider(clock);
        var key = "k3";
        await p.Store(key, 42, Cfg(TimeSpan.Zero));

        clock.Advance(TimeSpan.FromMilliseconds(150));

        var ok = p.TryGet<int>(key, out var value, Cfg(TimeSpan.Zero));
        Assert.True(ok);
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task Overwrite_Existing_Key_Replaces_Value()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var p = new InMemoryCacheModuleProvider(clock);
        var key = "k4";
        await p.Store(key, "a", Cfg(TimeSpan.FromSeconds(1)));
        await p.Store(key, "b", Cfg(TimeSpan.FromSeconds(1)));

        var ok = p.TryGet<string>(key, out var value, Cfg(TimeSpan.FromSeconds(1)));
        Assert.True(ok);
        Assert.Equal("b", value);
    }

    [Fact]
    public async Task Remove_Removes_Existing_Key_And_ReturnsTrue()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var p = new InMemoryCacheModuleProvider(clock);
        var key = "rm1";
        await p.Store(key, "val", Cfg(TimeSpan.FromSeconds(5)));

        var existed = p.Remove(key);
        Assert.True(existed);
        var ok = p.TryGet<string>(key, out _, Cfg(TimeSpan.FromSeconds(5)));
        Assert.False(ok);
    }

    [Fact]
    public void Remove_ReturnsFalse_When_Key_Not_Found()
    {
        var p = new InMemoryCacheModuleProvider(TimeProvider.System);
        var existed = p.Remove("missing-key");
        Assert.False(existed);
    }

    [Fact]
    public async Task Clear_Removes_All_Entries()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var p = new InMemoryCacheModuleProvider(clock);
        await p.Store("a", 1, Cfg(TimeSpan.FromSeconds(10)));
        await p.Store("b", 2, Cfg(TimeSpan.FromSeconds(10)));

        p.Clear();

        var okA = p.TryGet<int>("a", out var _, Cfg(TimeSpan.FromSeconds(10)));
        var okB = p.TryGet<int>("b", out var _, Cfg(TimeSpan.FromSeconds(10)));
        Assert.False(okA);
        Assert.False(okB);
    }
}
