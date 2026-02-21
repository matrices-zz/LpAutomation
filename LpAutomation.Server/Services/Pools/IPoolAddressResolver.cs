namespace LpAutomation.Server.Services.Pools;

public interface IPoolAddressResolver
{
    Task<string?> ResolveV3PoolAddressAsync(
        int chainId,
        string dex,
        string token0Address,
        string token1Address,
        int feeTier,
        CancellationToken ct);
}
