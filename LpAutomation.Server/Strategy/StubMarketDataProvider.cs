using LpAutomation.Core.Models;
using LpAutomation.Core.Strategy;

namespace LpAutomation.Server.Strategy;

// MVP: stubbed metrics so we can validate flow end-to-end.
// Later you'll swap this with Uniswap + price/vol analytics.
public sealed class StubMarketDataProvider : IMarketDataProvider
{
    private readonly Random _rng = new();

    public Task<PoolSnapshot> GetSnapshotAsync(PoolKey key, CancellationToken ct)
    {
        var snapshot = new PoolSnapshot(
            ChainId: key.ChainId,
            Token0: key.Token0 ?? "",
            Token1: key.Token1 ?? "",
            FeeTier: key.FeeTier,
            AsOfUtc: DateTimeOffset.UtcNow,
            Price: 1.0 + _rng.NextDouble(),
            VolNorm: _rng.NextDouble() * 0.25,
            TrendR2: _rng.NextDouble(),
            EmaSlopeAbs: _rng.NextDouble() * 0.02
        );

        return Task.FromResult(snapshot);
    }
}