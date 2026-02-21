using System;
using System.Collections.Generic;
using LpAutomation.Core.Models;

namespace LpAutomation.Core.Validation;

public sealed record AutoCorrection(string Path, string Message);
public sealed record AutoCorrectResult(StrategyConfigDocument Corrected, List<AutoCorrection> Corrections);

public static class StrategyConfigAutoCorrector
{
    public static AutoCorrectResult AutoCorrect(StrategyConfigDocument doc)
    {
        var corrections = new List<AutoCorrection>();
        var g = doc.Global;

        // Normalize weights (safe fix)
        var w = g.Scoring.Reinvest.Weights;
        (double rf, double ri) = NormalizePair(w.RewardFee, w.RewardInRange, "$.global.scoring.reinvest.weights.reward", corrections);
        (double rv, double rd, double rtp) = NormalizeTriple(w.RiskVol, w.RiskDrift, w.RiskTrendPenalty, "$.global.scoring.reinvest.weights.risk", corrections);

        var newW = w with { RewardFee = rf, RewardInRange = ri, RiskVol = rv, RiskDrift = rd, RiskTrendPenalty = rtp };
        if (!Equals(newW, w))
            g = g with { Scoring = g.Scoring with { Reinvest = g.Scoring.Reinvest with { Weights = newW } } };

        // Clamp 0..1 fields (safe fix)
        g = g with
        {
            Regime = g.Regime with
            {
                Trend = g.Regime.Trend with { R2Min = Clamp01(g.Regime.Trend.R2Min) },
                Sideways = g.Regime.Sideways with
                {
                    R2Max = Clamp01(g.Regime.Sideways.R2Max),
                    BandContainPctMin = Clamp01(g.Regime.Sideways.BandContainPctMin)
                },
                Volatile = g.Regime.Volatile with { R2Max = Clamp01(g.Regime.Volatile.R2Max) }
            },
            Hedging = g.Hedging with
            {
                ExposurePctTrigger = Clamp01(g.Hedging.ExposurePctTrigger),
                SuggestedHedgePct = g.Hedging.SuggestedHedgePct with
                {
                    TrendDown = g.Hedging.SuggestedHedgePct.TrendDown with
                    {
                        Min = Clamp01(g.Hedging.SuggestedHedgePct.TrendDown.Min),
                        Max = Clamp01(g.Hedging.SuggestedHedgePct.TrendDown.Max)
                    },
                    VolSpike = g.Hedging.SuggestedHedgePct.VolSpike with
                    {
                        Min = Clamp01(g.Hedging.SuggestedHedgePct.VolSpike.Min),
                        Max = Clamp01(g.Hedging.SuggestedHedgePct.VolSpike.Max)
                    }
                }
            }
        };

        // IMPORTANT: no auto-correct for money limits (maxNotional/maxApproval) or slippage.
        var corrected = doc with { Global = g, UpdatedUtc = DateTimeOffset.UtcNow };
        return new AutoCorrectResult(corrected, corrections);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

    private static (double a, double b) NormalizePair(double a, double b, string path, List<AutoCorrection> c)
    {
        var sum = a + b;
        if (sum <= 1e-9)
        {
            c.Add(new(path, "Reward weights were zero; reset to 0.5/0.5."));
            return (0.5, 0.5);
        }
        if (Math.Abs(sum - 1.0) > 0.01) c.Add(new(path, $"Normalized reward weights (sum was {sum:F4})."));
        return (a / sum, b / sum);
    }

    private static (double a, double b, double d) NormalizeTriple(double a, double b, double d, string path, List<AutoCorrection> c)
    {
        var sum = a + b + d;
        if (sum <= 1e-9)
        {
            c.Add(new(path, "Risk weights were zero; reset to 0.34/0.33/0.33."));
            return (0.34, 0.33, 0.33);
        }
        if (Math.Abs(sum - 1.0) > 0.01) c.Add(new(path, $"Normalized risk weights (sum was {sum:F4})."));
        return (a / sum, b / sum, d / sum);
    }
}
