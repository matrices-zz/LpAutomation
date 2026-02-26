using System;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public sealed class RecommendationRowVm
{
    public string Pool { get; init; } = "";
    public string PoolAddress { get; init; } = "";
    public string Dex { get; init; } = "";
    public int ChainId { get; init; }
    public string ChainLabel { get; init; } = "";
    public string FeeLabel { get; init; } = "";
    public string RegimeLabel { get; init; } = "";
    public int Reinvest { get; init; }
    public int Reallocate { get; init; }
    public DateTimeOffset UpdatedUtc { get; init; }
    public string UpdatedLabel { get; init; } = "";
    public string Summary { get; init; } = "";
}
