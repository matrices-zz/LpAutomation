namespace LpAutomation.Desktop.Avalonia.ViewModels;

public sealed class RecommendationRowVm
{
    public string Pool { get; init; } = "";
    public string RegimeLabel { get; init; } = "";
    public int Reinvest { get; init; }
    public int Reallocate { get; init; }
    public string Updated { get; init; } = "";
    public string Summary { get; init; } = "";
}
