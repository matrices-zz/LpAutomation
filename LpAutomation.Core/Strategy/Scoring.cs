using LpAutomation.Core.Models;

namespace LpAutomation.Core.Strategy;

public static class Scoring
{
    /// <summary>
    /// Composite score based on fee APR, volatility, trend strength, and risk mode.
    /// Returns a value normalized to 0–100.
    /// </summary>
    public static double CalculateCompositeScore(
    double volNorm,
    double trendR2,
    RiskMode risk)
    {
        double volStabilityScore = 1.0 / (1.0 + volNorm);

        double volWeight = risk switch
        {
            RiskMode.Aggressive => 0.15,
            RiskMode.Conservative => 0.50,
            _ => 0.30
        };

        double trendWeight = 1.0 - volWeight;

        double raw = (volStabilityScore * volWeight) + (trendR2 * trendWeight);
        return Math.Clamp(raw * 100.0, 0, 100);
    }

    /// <summary>
    /// Reinvest score: rewards stability (low vol, low trend, sideways regime).
    /// </summary>
    public static int ScoreReinvest(PoolSnapshot s, MarketRegime regime)
    {
        double composite = CalculateCompositeScore(
            volNorm: s.VolNorm,
            trendR2: s.TrendR2,
            risk: RiskMode.Balanced);

        composite += regime switch
        {
            MarketRegime.Sideways => +5,
            MarketRegime.Trending => -10,
            MarketRegime.Volatile => -20,
            _ => 0
        };

        return Math.Clamp((int)Math.Round(composite), 0, 100);
    }

    /// <summary>
    /// <summary>
    /// Reallocate score: favored when risk is rising (trending or volatile).
    /// </summary>
    public static int ScoreReallocate(PoolSnapshot s, MarketRegime regime)
    {
        double composite = CalculateCompositeScore(
            volNorm: s.VolNorm,
            trendR2: s.TrendR2,
            risk: RiskMode.Conservative);

        composite += regime switch
        {
            MarketRegime.Trending => +15,
            MarketRegime.Volatile => +30,
            _ => 0
        };

        return Math.Clamp((int)Math.Round(composite), 0, 100);
    }
}