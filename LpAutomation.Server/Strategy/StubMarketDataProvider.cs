using LpAutomation.Core.Models;
using LpAutomation.Core.Strategy;

namespace LpAutomation.Server.Strategy;

// MVP: stubbed metrics so we can validate flow end-to-end.
// Later you’ll swap this with Uniswap + price/vol analytics.
public sealed class StubMarketDataProvider : IMarketDataProvider
{
    private readonly Random _rng = new();

    public Task<PoolSnapshot> GetSnapshotAsync(PoolKey key, CancellationToken ct)
    {
        var vol = _rng.NextDouble() * 0.25;  // 0..0.25
        var r2 = _rng.NextDouble();         // 0..1
        var slope = _rng.NextDouble() * 0.02; // 0..0.02

        return Task.FromResult(new PoolSnapshot(
            key.ChainId, key.Token0 ?? "", key.Token1 ?? "", key.FeeTier,
            DateTimeOffset.UtcNow,
            Price: 1.0 + _rng.NextDouble(),
            VolNorm: vol,
            TrendR2: r2,
            EmaSlopeAbs: slope
        ));
    }
}
