using LpAutomation.Core.Models;

namespace LpAutomation.Core.Strategy;

public static class RegimeDetector
{
    public static MarketRegime Detect(
        PoolSnapshot s,
        double sidewaysVolMax,
        double sidewaysR2Max,
        double trendR2Min,
        double trendSlopeAbsMin,
        double volatileVolMin,
        double volatileR2Max)
    {
        // Volatile
        if (s.VolNorm >= volatileVolMin && s.TrendR2 <= volatileR2Max)
            return MarketRegime.Volatile;

        // Trending
        if (s.TrendR2 >= trendR2Min && s.EmaSlopeAbs >= trendSlopeAbsMin)
            return MarketRegime.Trending;

        // Sideways
        if (s.VolNorm <= sidewaysVolMax && s.TrendR2 <= sidewaysR2Max)
            return MarketRegime.Sideways;

        // Fallback
        return s.VolNorm >= volatileVolMin
            ? MarketRegime.Volatile
            : MarketRegime.Sideways;
    }
}