using System.Linq;
using LpAutomation.Core.Models;

namespace LpAutomation.Core.Resolve;

public static class StrategyConfigResolver
{
    public static GlobalStrategyConfig ResolveEffectiveForPool(StrategyConfigDocument doc, PoolKey pool)
    {
        var normalized = pool.Normalized();
        var match = doc.Overrides
            .Where(o => o.Enabled)
            .FirstOrDefault(o => o.PoolKey.Normalized().Equals(normalized));

        return match is null ? doc.Global : ResolveEffective(doc.Global, match);
    }

    public static GlobalStrategyConfig ResolveEffective(GlobalStrategyConfig global, PoolOverride ov)
    {
        var eff = global;
        var p = ov.Patch;

        if (p.Regime is not null) eff = eff with { Regime = Apply(eff.Regime, p.Regime) };
        if (p.Scoring is not null) eff = eff with { Scoring = Apply(eff.Scoring, p.Scoring) };
        if (p.Decisions is not null) eff = eff with { Decisions = Apply(eff.Decisions, p.Decisions) };
        if (p.Guardrails is not null) eff = eff with { Guardrails = Apply(eff.Guardrails, p.Guardrails) };
        if (p.Hedging is not null) eff = eff with { Hedging = Apply(eff.Hedging, p.Hedging) };

        return eff;
    }

    private static RegimeConfig Apply(RegimeConfig b, RegimeConfigPatch p) => b with
    {
        Trend = p.Trend is null ? b.Trend : b.Trend with
        {
            R2Min = p.Trend.R2Min ?? b.Trend.R2Min,
            EmaSlopeAbsMin = p.Trend.EmaSlopeAbsMin ?? b.Trend.EmaSlopeAbsMin,
            WindowHours = p.Trend.WindowHours ?? b.Trend.WindowHours
        },
        Sideways = p.Sideways is null ? b.Sideways : b.Sideways with
        {
            VolNormMax = p.Sideways.VolNormMax ?? b.Sideways.VolNormMax,
            R2Max = p.Sideways.R2Max ?? b.Sideways.R2Max,
            BandContainPctMin = p.Sideways.BandContainPctMin ?? b.Sideways.BandContainPctMin,
            WindowHours = p.Sideways.WindowHours ?? b.Sideways.WindowHours
        },
        Volatile = p.Volatile is null ? b.Volatile : b.Volatile with
        {
            VolNormMin = p.Volatile.VolNormMin ?? b.Volatile.VolNormMin,
            R2Max = p.Volatile.R2Max ?? b.Volatile.R2Max
        },
        PriorityOrder = p.PriorityOrder ?? b.PriorityOrder
    };

    private static ScoringConfig Apply(ScoringConfig b, ScoringConfigPatch p)
        => b with { Reinvest = p.Reinvest is null ? b.Reinvest : Apply(b.Reinvest, p.Reinvest) };

    private static ReinvestScoringConfig Apply(ReinvestScoringConfig b, ReinvestScoringConfigPatch p) => b with
    {
        FeeApr = p.FeeApr is null ? b.FeeApr : b.FeeApr with { Min = p.FeeApr.Min ?? b.FeeApr.Min, Max = p.FeeApr.Max ?? b.FeeApr.Max },
        InRangePct = p.InRangePct is null ? b.InRangePct : b.InRangePct with { Min = p.InRangePct.Min ?? b.InRangePct.Min, Max = p.InRangePct.Max ?? b.InRangePct.Max },
        VolNorm = p.VolNorm is null ? b.VolNorm : b.VolNorm with { Min = p.VolNorm.Min ?? b.VolNorm.Min, Max = p.VolNorm.Max ?? b.VolNorm.Max },
        DriftTicksPerHour = p.DriftTicksPerHour is null ? b.DriftTicksPerHour : b.DriftTicksPerHour with
        {
            MinMultiplierOfTickSpacing = p.DriftTicksPerHour.MinMultiplierOfTickSpacing ?? b.DriftTicksPerHour.MinMultiplierOfTickSpacing,
            MaxMultiplierOfTickSpacing = p.DriftTicksPerHour.MaxMultiplierOfTickSpacing ?? b.DriftTicksPerHour.MaxMultiplierOfTickSpacing
        },
        Weights = p.Weights is null ? b.Weights : b.Weights with
        {
            RewardFee = p.Weights.RewardFee ?? b.Weights.RewardFee,
            RewardInRange = p.Weights.RewardInRange ?? b.Weights.RewardInRange,
            RiskVol = p.Weights.RiskVol ?? b.Weights.RiskVol,
            RiskDrift = p.Weights.RiskDrift ?? b.Weights.RiskDrift,
            RiskTrendPenalty = p.Weights.RiskTrendPenalty ?? b.Weights.RiskTrendPenalty
        },
        TrendPenalty = p.TrendPenalty is null ? b.TrendPenalty : b.TrendPenalty with
        {
            Sideways = p.TrendPenalty.Sideways ?? b.TrendPenalty.Sideways,
            Trending = p.TrendPenalty.Trending ?? b.TrendPenalty.Trending,
            Volatile = p.TrendPenalty.Volatile ?? b.TrendPenalty.Volatile
        },
        ScoreCentering = p.ScoreCentering ?? b.ScoreCentering
    };

    private static DecisionConfig Apply(DecisionConfig b, DecisionConfigPatch p) => b with
    {
        CompoundScoreMin = p.CompoundScoreMin ?? b.CompoundScoreMin,
        ReallocateScoreMax = p.ReallocateScoreMax ?? b.ReallocateScoreMax,
        MinHoursBetweenCompounds = p.MinHoursBetweenCompounds ?? b.MinHoursBetweenCompounds,
        StaleAfterSeconds = p.StaleAfterSeconds ?? b.StaleAfterSeconds
    };

    private static GuardrailsConfig Apply(GuardrailsConfig b, GuardrailsConfigPatch p) => b with
    {
        MaxSlippageBps = p.MaxSlippageBps ?? b.MaxSlippageBps,
        MaxNotionalUsdPerProposal = p.MaxNotionalUsdPerProposal ?? b.MaxNotionalUsdPerProposal,
        MaxGasFeeGwei = p.MaxGasFeeGwei ?? b.MaxGasFeeGwei,
        DeadlineSecondsAtExecution = p.DeadlineSecondsAtExecution ?? b.DeadlineSecondsAtExecution,
        AllowApprovals = p.AllowApprovals ?? b.AllowApprovals,
        MaxApprovalUsd = p.MaxApprovalUsd ?? b.MaxApprovalUsd
    };

    private static HedgingConfig Apply(HedgingConfig b, HedgingConfigPatch p) => b with
    {
        Enabled = p.Enabled ?? b.Enabled,
        ExposurePctTrigger = p.ExposurePctTrigger ?? b.ExposurePctTrigger,
        TrendDown = p.TrendDown is null ? b.TrendDown : b.TrendDown with
        {
            R2Min = p.TrendDown.R2Min ?? b.TrendDown.R2Min,
            EmaSlopeMax = p.TrendDown.EmaSlopeMax ?? b.TrendDown.EmaSlopeMax
        },
        VolSpike = p.VolSpike is null ? b.VolSpike : b.VolSpike with
        {
            Vol1hOverVol24hMin = p.VolSpike.Vol1hOverVol24hMin ?? b.VolSpike.Vol1hOverVol24hMin
        },
        SuggestedHedgePct = p.SuggestedHedgePct is null ? b.SuggestedHedgePct : b.SuggestedHedgePct with
        {
            TrendDown = p.SuggestedHedgePct.TrendDown is null ? b.SuggestedHedgePct.TrendDown : b.SuggestedHedgePct.TrendDown with
            {
                Min = p.SuggestedHedgePct.TrendDown.Min ?? b.SuggestedHedgePct.TrendDown.Min,
                Max = p.SuggestedHedgePct.TrendDown.Max ?? b.SuggestedHedgePct.TrendDown.Max
            },
            VolSpike = p.SuggestedHedgePct.VolSpike is null ? b.SuggestedHedgePct.VolSpike : b.SuggestedHedgePct.VolSpike with
            {
                Min = p.SuggestedHedgePct.VolSpike.Min ?? b.SuggestedHedgePct.VolSpike.Min,
                Max = p.SuggestedHedgePct.VolSpike.Max ?? b.SuggestedHedgePct.VolSpike.Max
            }
        }
    };
}
