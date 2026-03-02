using LpAutomation.Core.Models;

namespace LpAutomation.Core.Strategy;

public static class RegimeDetector
{
    /// <summary>
    /// Primary classifier used by the Server to determine the current market state.
    /// This fixes the CS0117 error in StrategyEngineHostedService.cs.
    /// </summary>
    public static MarketRegime Detect(
        PoolSnapshot s,
        double sidewaysVolMax,
        double sidewaysR2Max,
        double trendR2Min,
        double trendSlopeAbsMin,
        double volatileVolMin,
        double volatileR2Max)
    {
        // 1. Volatile: High normalized volatility with low trend consistency
        if (s.VolNorm >= volatileVolMin && s.TrendR2 <= volatileR2Max)
            return MarketRegime.Volatile;

        // 2. Trending: High R-Squared and significant slope
        if (s.TrendR2 >= trendR2Min && s.EmaSlopeAbs >= trendSlopeAbsMin)
            return MarketRegime.Trending;

        // 3. Sideways: Low volatility and low trend consistency
        if (s.VolNorm <= sidewaysVolMax && s.TrendR2 <= sidewaysR2Max)
            return MarketRegime.Sideways;

        // Fallback: If it doesn't fit a clean bucket, use volatility as the primary pivot
        return s.VolNorm >= volatileVolMin ? MarketRegime.Volatile : MarketRegime.Sideways;
    }

    /// <summary>
    /// Blends the detected regime with risk preferences to suggest a specific LP profile.
    /// This is the "Decision Support" layer.
    /// </summary>
    public static StrategyProfile SuggestProfile(MarketRegime regime, double volNorm, RiskMode risk)
    {
        return regime switch
        {
            MarketRegime.Volatile => risk == RiskMode.Aggressive
                ? StrategyProfile.Laddered
                : StrategyProfile.VolatilityBuffered,

            MarketRegime.Trending => risk == RiskMode.Conservative
                ? StrategyProfile.ExitStrategy
                : StrategyProfile.Baseline,

            MarketRegime.Sideways => volNorm < 0.02
                ? StrategyProfile.TightFarming
                : StrategyProfile.GeometricMeanCore,

            _ => StrategyProfile.Baseline
        };
    }
}