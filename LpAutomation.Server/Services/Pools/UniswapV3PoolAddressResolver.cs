using Microsoft.Extensions.Caching.Memory;

namespace LpAutomation.Server.Services.Pools;

public sealed class UniswapV3PoolAddressResolver : IPoolAddressResolver
{
    private readonly IMemoryCache _cache;
    private readonly IOnChainPoolFactoryClient _factory; // your abstraction; see note below

    public UniswapV3PoolAddressResolver(IMemoryCache cache, IOnChainPoolFactoryClient factory)
    {
        _cache = cache;
        _factory = factory;
    }

    public Task<string?> ResolveV3PoolAddressAsync(
        int chainId,
        string dex,
        string token0Address,
        string token1Address,
        int feeTier,
        CancellationToken ct)
    {
        // canonicalize ordering by address so cache keys are stable
        var (a, b) = Order(token0Address, token1Address);

        var key = $"v3pool:{chainId}:{dex}:{a}:{b}:{feeTier}";
        return _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

            // This abstraction should:
            // - know factory address per chain/dex
            // - call getPool(a,b,feeTier)
            // - return 0x000... if none
            var pool = await _factory.GetPoolAsync(chainId, dex, a, b, feeTier, ct);

            if (string.IsNullOrWhiteSpace(pool) || IsZero(pool))
            {
                // Don't cache misses forever; short TTL for nulls
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return null;
            }

            return pool;
        })!;
    }

    private static (string, string) Order(string x, string y)
        => string.Compare(x, y, StringComparison.OrdinalIgnoreCase) <= 0 ? (x, y) : (y, x);

    private static bool IsZero(string addr)
        => addr.Equals("0x0000000000000000000000000000000000000000", StringComparison.OrdinalIgnoreCase);
}
