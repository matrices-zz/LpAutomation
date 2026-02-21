using System.Linq;
using LpAutomation.Core.Models;

namespace LpAutomation.Core.Validation;

public static class StrategyConfigValidator
{
    public static ConfigValidationResult Validate(StrategyConfigDocument doc)
    {
        var r = new ConfigValidationResult();

        if (doc.SchemaVersion != 1)
            r.Error("$.schemaVersion", "Unsupported schemaVersion (expected 1).");

        ValidateGlobal(r, doc.Global, "$.global");
        ValidateOverrides(r, doc);

        return r;
    }

    private static void ValidateOverrides(ConfigValidationResult r, StrategyConfigDocument doc)
    {
        var keys = doc.Overrides
            .Where(o => o.Enabled)
            .Select(o => o.PoolKey.Normalized())
            .Select(k => $"{k.ChainId}:{k.Token0}:{k.Token1}:{k.FeeTier}")
            .ToList();

        foreach (var dupe in keys.GroupBy(x => x).Where(g => g.Count() > 1))
            r.Error("$.overrides", $"Duplicate enabled override for poolKey: {dupe.Key}");

        for (int i = 0; i < doc.Overrides.Count; i++)
        {
            var ov = doc.Overrides[i];
            var p = $"$.overrides[{i}]";

            if (ov.PoolKey.ChainId <= 0) r.Error($"{p}.poolKey.chainId", "chainId must be > 0.");
            if (string.IsNullOrWhiteSpace(ov.PoolKey.Token0)) r.Error($"{p}.poolKey.token0", "token0 required.");
            if (string.IsNullOrWhiteSpace(ov.PoolKey.Token1)) r.Error($"{p}.poolKey.token1", "token1 required.");
            if (ov.PoolKey.FeeTier <= 0) r.Error($"{p}.poolKey.feeTier", "feeTier must be > 0.");

            // Patch range checks (only for values present)
            if (ov.Patch.Guardrails?.MaxSlippageBps is int ms && (ms < 0 || ms > 500))
                r.Error($"{p}.patch.guardrails.maxSlippageBps", "Must be in [0..500] bps.");

            if (ov.Patch.Decisions?.CompoundScoreMin is int cs && (cs < 0 || cs > 100))
                r.Error($"{p}.patch.decisions.compoundScoreMin", "Must be in [0..100].");
        }
    }

    private static void ValidateGlobal(ConfigValidationResult r, GlobalStrategyConfig g, string p)
    {
        // regime 0..1 and positive checks
        Check01(r, $"{p}.regime.trend.r2Min", g.Regime.Trend.R2Min);
        if (g.Regime.Trend.EmaSlopeAbsMin <= 0) r.Error($"{p}.regime.trend.emaSlopeAbsMin", "Must be > 0.");
        CheckPos(r, $"{p}.regime.trend.windowHours", g.Regime.Trend.WindowHours);

        Check01(r, $"{p}.regime.sideways.r2Max", g.Regime.Sideways.R2Max);
        Check01(r, $"{p}.regime.sideways.bandContainPctMin", g.Regime.Sideways.BandContainPctMin);
        if (g.Regime.Sideways.VolNormMax <= 0) r.Error($"{p}.regime.sideways.volNormMax", "Must be > 0.");
        CheckPos(r, $"{p}.regime.sideways.windowHours", g.Regime.Sideways.WindowHours);

        if (g.Regime.Volatile.VolNormMin <= 0) r.Error($"{p}.regime.volatile.volNormMin", "Must be > 0.");
        Check01(r, $"{p}.regime.volatile.r2Max", g.Regime.Volatile.R2Max);

        if (g.Regime.PriorityOrder.Distinct().Count() != 3)
            r.Error($"{p}.regime.priorityOrder", "Must contain TRENDING, VOLATILE, SIDEWAYS exactly once each.");

        // scoring ranges
        CheckMinMax(r, $"{p}.scoring.reinvest.feeApr", g.Scoring.Reinvest.FeeApr.Min, g.Scoring.Reinvest.FeeApr.Max);
        CheckMinMax(r, $"{p}.scoring.reinvest.volNorm", g.Scoring.Reinvest.VolNorm.Min, g.Scoring.Reinvest.VolNorm.Max);

        Check01(r, $"{p}.scoring.reinvest.inRangePct.min", g.Scoring.Reinvest.InRangePct.Min);
        Check01(r, $"{p}.scoring.reinvest.inRangePct.max", g.Scoring.Reinvest.InRangePct.Max);
        CheckMinMax(r, $"{p}.scoring.reinvest.inRangePct", g.Scoring.Reinvest.InRangePct.Min, g.Scoring.Reinvest.InRangePct.Max);

        // drift
        if (g.Scoring.Reinvest.DriftTicksPerHour.MinMultiplierOfTickSpacing < 1)
            r.Error($"{p}.scoring.reinvest.driftTicksPerHour.minMultiplierOfTickSpacing", "Must be >= 1.");
        if (g.Scoring.Reinvest.DriftTicksPerHour.MaxMultiplierOfTickSpacing <= g.Scoring.Reinvest.DriftTicksPerHour.MinMultiplierOfTickSpacing)
            r.Error($"{p}.scoring.reinvest.driftTicksPerHour.maxMultiplierOfTickSpacing", "Must be > min.");

        // decision thresholds
        CheckScore(r, $"{p}.decisions.compoundScoreMin", g.Decisions.CompoundScoreMin);
        CheckScore(r, $"{p}.decisions.reallocateScoreMax", g.Decisions.ReallocateScoreMax);
        if (g.Decisions.ReallocateScoreMax >= g.Decisions.CompoundScoreMin)
            r.Warn($"{p}.decisions", "reallocateScoreMax >= compoundScoreMin shrinks/removes HOLD band.");

        CheckPos(r, $"{p}.decisions.minHoursBetweenCompounds", g.Decisions.MinHoursBetweenCompounds);
        CheckPos(r, $"{p}.decisions.staleAfterSeconds", g.Decisions.StaleAfterSeconds);

        // guardrails
        if (g.Guardrails.MaxSlippageBps < 0 || g.Guardrails.MaxSlippageBps > 500)
            r.Error($"{p}.guardrails.maxSlippageBps", "Must be in [0..500] bps.");
        if (g.Guardrails.DeadlineSecondsAtExecution < 60 || g.Guardrails.DeadlineSecondsAtExecution > 3600)
            r.Error($"{p}.guardrails.deadlineSecondsAtExecution", "Must be in [60..3600] seconds.");
        if (g.Guardrails.MaxNotionalUsdPerProposal <= 0)
            r.Error($"{p}.guardrails.maxNotionalUsdPerProposal", "Must be > 0.");
        if (g.Guardrails.AllowApprovals && g.Guardrails.MaxApprovalUsd <= 0)
            r.Error($"{p}.guardrails.maxApprovalUsd", "Must be > 0 when allowApprovals=true.");

        // hedging
        Check01(r, $"{p}.hedging.exposurePctTrigger", g.Hedging.ExposurePctTrigger);
        Check01(r, $"{p}.hedging.suggestedHedgePct.trendDown.min", g.Hedging.SuggestedHedgePct.TrendDown.Min);
        Check01(r, $"{p}.hedging.suggestedHedgePct.trendDown.max", g.Hedging.SuggestedHedgePct.TrendDown.Max);
        if (g.Hedging.SuggestedHedgePct.TrendDown.Max < g.Hedging.SuggestedHedgePct.TrendDown.Min)
            r.Error($"{p}.hedging.suggestedHedgePct.trendDown", "Max must be >= Min.");
    }

    private static void Check01(ConfigValidationResult r, string path, double v)
    { if (v < 0 || v > 1) r.Error(path, "Must be between 0 and 1."); }

    private static void CheckPos(ConfigValidationResult r, string path, int v)
    { if (v <= 0) r.Error(path, "Must be > 0."); }

    private static void CheckScore(ConfigValidationResult r, string path, int v)
    { if (v < 0 || v > 100) r.Error(path, "Must be between 0 and 100."); }

    private static void CheckMinMax(ConfigValidationResult r, string path, double min, double max)
    { if (max <= min) r.Error(path, "Max must be > Min."); }
}
