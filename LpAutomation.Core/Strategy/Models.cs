namespace LpAutomation.Core.Strategy;

public enum MarketRegime
{
    Sideways,
    Trending,
    Volatile
}

public sealed record PoolSnapshot(
    long ChainId,
    string Token0,
    string Token1,
    int FeeTier,
    DateTimeOffset AsOfUtc,
    double Price,                 // simplified for MVP
    double VolNorm,               // e.g., ATR/price
    double TrendR2,               // regression R^2
    double EmaSlopeAbs            // abs slope
);

public sealed record Recommendation(
    Guid Id,
    DateTimeOffset CreatedUtc,
    long ChainId,
    string Token0,
    string Token1,
    int FeeTier,
    MarketRegime Regime,
    int ReinvestScore,            // 0..100
    int ReallocateScore,          // 0..100
    string Summary,
    string? DetailsJson = null    // optional for deep debug
);
