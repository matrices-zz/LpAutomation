using System;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public sealed class RecommendationRowVm
{
    public string Pool { get; init; } = "";
    public string PoolAddress { get; init; } = "";
    public string Dex { get; init; } = "";
    public int ChainId { get; init; }
    public string ChainLabel { get; init; } = "";
    public int FeeTier { get; init; }
    public string FeeLabel { get; init; } = "";
    public string RegimeLabel { get; init; } = "";

    public int Reinvest { get; init; }
    public string ReinvestBrush { get; init; } = "#64748B";

    public int Reallocate { get; init; }
    public string ReallocateBrush { get; init; } = "#64748B";

    public string DecisionLabel { get; init; } = "Hold";
    public int DecisionConfidence { get; init; }
    public string DecisionBrush { get; init; } = "#64748B";
    public string DecisionConfidenceTooltip { get; init; } = "";

    public int MarketHeat { get; init; }
    public string MarketHeatTooltip { get; init; } = "";

    public int OpportunityScore { get; init; }
    public string OpportunityTooltip { get; init; } = "";

    public DateTimeOffset UpdatedUtc { get; init; }
    public string UpdatedLabel { get; init; } = "";
    public string Summary { get; init; } = "";
}
