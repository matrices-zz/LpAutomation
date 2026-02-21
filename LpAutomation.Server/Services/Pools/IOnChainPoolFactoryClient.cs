namespace LpAutomation.Server.Services.Pools;

public interface IOnChainPoolFactoryClient
{
    Task<string?> GetPoolAsync(int chainId, string dex, string token0, string token1, int feeTier, CancellationToken ct);
}
