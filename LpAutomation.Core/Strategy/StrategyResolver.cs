using LpAutomation.Core.Models;

namespace LpAutomation.Core.Strategy;

public static class StrategyResolver
{
    public static StrategyProfile RecommendProfile(MarketRegime regime, RiskMode riskMode, double volNorm, double feeApr)
    {
        // Conservative: prioritize protection over fee capture
        if (riskMode == RiskMode.Conservative)
        {
            if (volNorm > 1.5) return StrategyProfile.VolatilityBuffered;
            if (regime == MarketRegime.Trending) return StrategyProfile.ExitStrategy;
            return StrategyProfile.GeometricMeanCore;
        }

        // Aggressive: prioritize fee capture
        if (riskMode == RiskMode.Aggressive)
        {
            if (regime == MarketRegime.Sideways && volNorm < 1.0) return StrategyProfile.TightFarming;
            if (volNorm > 1.5) return StrategyProfile.Laddered;
            return StrategyProfile.TightFarming;
        }

        // Balanced (default)
        if (volNorm > 1.5) return StrategyProfile.VolatilityBuffered;
        if (regime == MarketRegime.Sideways && volNorm < 1.0) return StrategyProfile.TightFarming;
        if (regime == MarketRegime.Trending && feeApr > 0.2) return StrategyProfile.ExitStrategy;
        return StrategyProfile.Baseline;
    }
}