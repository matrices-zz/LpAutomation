using System;
using System.Collections.Generic;

namespace LpAutomation.Core.Models;

public enum RegimeType { SIDEWAYS, TRENDING, VOLATILE }

public enum RiskMode
{
    Conservative, // High IL protection, wider buffers
    Balanced,     // Standard risk/reward
    Aggressive    // Tight ranges, high fee focus, frequent rebalancing
}

public enum StrategyProfile
{
    Baseline,           // Standard market-neutral
    TightFarming,       // Narrow ranges for high volume
    ExitStrategy,       // Capital preservation / Range exit
    VolatilityBuffered, // Wide ranges to absorb chop
    GeometricMeanCore,  // Targeting mid-market mean reversion
    Laddered            // Multi-tier liquidity distribution
}

public record ActionProposal(
    string ActionType,      // "Rebalance", "Reinvest", "Exit", "Hedge"
    double CurrentRangeMin,
    double CurrentRangeMax,
    double NewRangeMin,
    double NewRangeMax,
    string Reason,          // e.g., "Volatility spike detected, moving to VolatilityBuffered"
    double ExpectedImpactPct
);

public sealed class StrategyProfileSettings
{
    public StrategyProfile Profile { get; init; }
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";

    // Profile-specific overrides for the math
    public double? WidthPct { get; init; }
    public double? WeightFeeApr { get; init; }
    public double? WeightRiskVol { get; init; }
    // ... other specific knobs for this strategy
}

public sealed record StrategyConfigDocument
{
    public int SchemaVersion { get; init; } = 1;
    public Guid ConfigId { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Default Strategy Config";
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; init; } = DateTimeOffset.UtcNow;

    public GlobalStrategyConfig Global { get; init; } = new();
    public List<PoolOverride> Overrides { get; init; } = new();
}

public sealed record GlobalStrategyConfig
{
    public RegimeConfig Regime { get; init; } = new();
    public ScoringConfig Scoring { get; init; } = new();
    public DecisionConfig Decisions { get; init; } = new();
    public GuardrailsConfig Guardrails { get; init; } = new();
    public HedgingConfig Hedging { get; init; } = new();
}

public sealed record PoolOverride
{
    public Guid OverrideId { get; init; } = Guid.NewGuid();
    public PoolKey PoolKey { get; init; } = new();
    public bool Enabled { get; init; } = true;
    public string Notes { get; init; } = "";
    public GlobalStrategyConfigPatch Patch { get; init; } = new();
}

public sealed record PoolKey
{
    public long ChainId { get; init; } = 1;
    public string Token0 { get; init; } = "";
    public string Token1 { get; init; } = "";
    public int FeeTier { get; init; } = 500;

    public PoolKey Normalized()
    {
        var a = (Token0 ?? "").Trim().ToLowerInvariant();
        var b = (Token1 ?? "").Trim().ToLowerInvariant();
        return string.CompareOrdinal(a, b) <= 0
            ? this with { Token0 = a, Token1 = b }
            : this with { Token0 = b, Token1 = a };
    }
}

/* ---------------- Regime ---------------- */

public sealed record RegimeConfig
{
    public TrendRegimeConfig Trend { get; init; } = new();
    public SidewaysRegimeConfig Sideways { get; init; } = new();
    public VolatileRegimeConfig Volatile { get; init; } = new();
    public List<RegimeType> PriorityOrder { get; init; } =
        new() { RegimeType.TRENDING, RegimeType.VOLATILE, RegimeType.SIDEWAYS };
}

public sealed record TrendRegimeConfig
{
    public double R2Min { get; init; } = 0.55;
    public double EmaSlopeAbsMin { get; init; } = 0.004;
    public int WindowHours { get; init; } = 6;
}

public sealed record SidewaysRegimeConfig
{
    public double VolNormMax { get; init; } = 1.00;
    public double R2Max { get; init; } = 0.45;
    public double BandContainPctMin { get; init; } = 0.70;
    public int WindowHours { get; init; } = 6;
}

public sealed record VolatileRegimeConfig
{
    public double VolNormMin { get; init; } = 1.30;
    public double R2Max { get; init; } = 0.50;
}

/* ---------------- Scoring ---------------- */

public sealed record ScoringConfig
{
    public ReinvestScoringConfig Reinvest { get; init; } = new();
}

public sealed record RangeDouble
{
    public double Min { get; init; }
    public double Max { get; init; }
}

public sealed record DriftTicksPerHourConfig
{
    public int MinMultiplierOfTickSpacing { get; init; } = 5;
    public int MaxMultiplierOfTickSpacing { get; init; } = 40;
}

public sealed record ReinvestWeights
{
    public double RewardFee { get; init; } = 0.60;
    public double RewardInRange { get; init; } = 0.40;
    public double RiskVol { get; init; } = 0.45;
    public double RiskDrift { get; init; } = 0.35;
    public double RiskTrendPenalty { get; init; } = 0.20;
}

public sealed record TrendPenaltyConfig
{
    public int Sideways { get; init; } = 0;
    public int Trending { get; init; } = 60;
    public int Volatile { get; init; } = 80;

    public int For(RegimeType r) => r switch
    {
        RegimeType.SIDEWAYS => Sideways,
        RegimeType.TRENDING => Trending,
        RegimeType.VOLATILE => Volatile,
        _ => Volatile
    };
}

public sealed record ReinvestScoringConfig
{
    public RangeDouble FeeApr { get; init; } = new() { Min = 0.10, Max = 0.80 };
    public RangeDouble InRangePct { get; init; } = new() { Min = 0.40, Max = 0.90 };
    public RangeDouble VolNorm { get; init; } = new() { Min = 0.80, Max = 1.80 };

    public DriftTicksPerHourConfig DriftTicksPerHour { get; init; } = new();
    public ReinvestWeights Weights { get; init; } = new();
    public TrendPenaltyConfig TrendPenalty { get; init; } = new();

    public int ScoreCentering { get; init; } = 50;
}

/* ---------------- Decisions/Guardrails/Hedging ---------------- */

public sealed record DecisionConfig
{
    public int CompoundScoreMin { get; init; } = 70;
    public int ReallocateScoreMax { get; init; } = 40;
    public int MinHoursBetweenCompounds { get; init; } = 6;
    public int StaleAfterSeconds { get; init; } = 3600;
}

public sealed record GuardrailsConfig
{
    public int MaxSlippageBps { get; init; } = 50;
    public decimal MaxNotionalUsdPerProposal { get; init; } = 2500m;
    public int MaxGasFeeGwei { get; init; } = 40;
    public int DeadlineSecondsAtExecution { get; init; } = 600;
    public bool AllowApprovals { get; init; } = true;
    public decimal MaxApprovalUsd { get; init; } = 5000m;
}

public sealed record HedgingConfig
{
    public bool Enabled { get; init; } = true;
    public double ExposurePctTrigger { get; init; } = 0.35;
    public TrendDownHedgeConfig TrendDown { get; init; } = new();
    public VolSpikeHedgeConfig VolSpike { get; init; } = new();
    public SuggestedHedgePctConfig SuggestedHedgePct { get; init; } = new();
}

public sealed record TrendDownHedgeConfig
{
    public double R2Min { get; init; } = 0.60;
    public double EmaSlopeMax { get; init; } = -0.004;
}

public sealed record VolSpikeHedgeConfig
{
    public double Vol1hOverVol24hMin { get; init; } = 1.80;
}

public sealed record PctRange
{
    public double Min { get; init; } = 0.25;
    public double Max { get; init; } = 0.50;
}

public sealed record SuggestedHedgePctConfig
{
    public PctRange TrendDown { get; init; } = new() { Min = 0.25, Max = 0.50 };
    public PctRange VolSpike { get; init; } = new() { Min = 0.10, Max = 0.25 };
}

/* ---------------- Patch Types ---------------- */

public sealed record GlobalStrategyConfigPatch
{
    public RegimeConfigPatch? Regime { get; init; }
    public ScoringConfigPatch? Scoring { get; init; }
    public DecisionConfigPatch? Decisions { get; init; }
    public GuardrailsConfigPatch? Guardrails { get; init; }
    public HedgingConfigPatch? Hedging { get; init; }
}

public sealed record RegimeConfigPatch
{
    public TrendRegimeConfigPatch? Trend { get; init; }
    public SidewaysRegimeConfigPatch? Sideways { get; init; }
    public VolatileRegimeConfigPatch? Volatile { get; init; }
    public List<RegimeType>? PriorityOrder { get; init; } // replace list
}

public sealed record TrendRegimeConfigPatch
{
    public double? R2Min { get; init; }
    public double? EmaSlopeAbsMin { get; init; }
    public int? WindowHours { get; init; }
}

public sealed record SidewaysRegimeConfigPatch
{
    public double? VolNormMax { get; init; }
    public double? R2Max { get; init; }
    public double? BandContainPctMin { get; init; }
    public int? WindowHours { get; init; }
}

public sealed record VolatileRegimeConfigPatch
{
    public double? VolNormMin { get; init; }
    public double? R2Max { get; init; }
}

public sealed record ScoringConfigPatch
{
    public ReinvestScoringConfigPatch? Reinvest { get; init; }
}

public sealed record RangeDoublePatch
{
    public double? Min { get; init; }
    public double? Max { get; init; }
}

public sealed record DriftTicksPerHourConfigPatch
{
    public int? MinMultiplierOfTickSpacing { get; init; }
    public int? MaxMultiplierOfTickSpacing { get; init; }
}

public sealed record ReinvestWeightsPatch
{
    public double? RewardFee { get; init; }
    public double? RewardInRange { get; init; }
    public double? RiskVol { get; init; }
    public double? RiskDrift { get; init; }
    public double? RiskTrendPenalty { get; init; }
}

public sealed record TrendPenaltyConfigPatch
{
    public int? Sideways { get; init; }
    public int? Trending { get; init; }
    public int? Volatile { get; init; }
}

public sealed record ReinvestScoringConfigPatch
{
    public RangeDoublePatch? FeeApr { get; init; }
    public RangeDoublePatch? InRangePct { get; init; }
    public RangeDoublePatch? VolNorm { get; init; }
    public DriftTicksPerHourConfigPatch? DriftTicksPerHour { get; init; }
    public ReinvestWeightsPatch? Weights { get; init; }
    public TrendPenaltyConfigPatch? TrendPenalty { get; init; }
    public int? ScoreCentering { get; init; }
}

public sealed record DecisionConfigPatch
{
    public int? CompoundScoreMin { get; init; }
    public int? ReallocateScoreMax { get; init; }
    public int? MinHoursBetweenCompounds { get; init; }
    public int? StaleAfterSeconds { get; init; }
}

public sealed record GuardrailsConfigPatch
{
    public int? MaxSlippageBps { get; init; }
    public decimal? MaxNotionalUsdPerProposal { get; init; }
    public int? MaxGasFeeGwei { get; init; }
    public int? DeadlineSecondsAtExecution { get; init; }
    public bool? AllowApprovals { get; init; }
    public decimal? MaxApprovalUsd { get; init; }
}

public sealed record HedgingConfigPatch
{
    public bool? Enabled { get; init; }
    public double? ExposurePctTrigger { get; init; }
    public TrendDownHedgeConfigPatch? TrendDown { get; init; }
    public VolSpikeHedgeConfigPatch? VolSpike { get; init; }
    public SuggestedHedgePctConfigPatch? SuggestedHedgePct { get; init; }
}

public sealed record TrendDownHedgeConfigPatch
{
    public double? R2Min { get; init; }
    public double? EmaSlopeMax { get; init; }
}

public sealed record VolSpikeHedgeConfigPatch
{
    public double? Vol1hOverVol24hMin { get; init; }
}

public sealed record PctRangePatch
{
    public double? Min { get; init; }
    public double? Max { get; init; }
}

public sealed record SuggestedHedgePctConfigPatch
{
    public PctRangePatch? TrendDown { get; init; }
    public PctRangePatch? VolSpike { get; init; }
}
