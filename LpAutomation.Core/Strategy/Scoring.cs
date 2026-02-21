namespace LpAutomation.Core.Strategy;

public static class Scoring
{
    // very MVP scoring: tune later via config (we’ll wire that next)
    public static int ScoreReinvest(PoolSnapshot s, MarketRegime regime)
    {
        // Reward: lower vol, higher stability
        // Penalize: trending + volatile
        var baseScore = 70;

        if (regime == MarketRegime.Sideways) baseScore += 10;
        if (regime == MarketRegime.Trending) baseScore -= 15;
        if (regime == MarketRegime.Volatile) baseScore -= 25;

        // VolNorm adjustment (assumes roughly 0..0.2 typical; clamp defensively)
        var volPenalty = (int)Math.Round(200 * Math.Clamp(s.VolNorm, 0, 0.5)); // 0..100
        var score = baseScore - volPenalty / 2;

        return Math.Clamp(score, 0, 100);
    }

    public static int ScoreReallocate(PoolSnapshot s, MarketRegime regime)
    {
        // Reallocate becomes attractive when trending/volatile risk rises
        var score = 30;

        if (regime == MarketRegime.Trending) score += 25;
        if (regime == MarketRegime.Volatile) score += 35;

        // Higher vol increases reallocate
        score += (int)Math.Round(150 * Math.Clamp(s.VolNorm, 0, 0.5)); // 0..75

        return Math.Clamp(score, 0, 100);
    }
}
