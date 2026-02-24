using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Desktop.Avalonia.Services;
using System;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class RecommendationsPageViewModel : ObservableObject
{
    private readonly RecommendationsApiClient _api;

    public RecommendationsPageViewModel(RecommendationsApiClient api)
    {
        _api = api;
        Rows = new ObservableCollection<RecommendationRowVm>();
        Status = "Ready";
        CountLabel = "Loaded 0 pools";
    }

    public ObservableCollection<RecommendationRowVm> Rows { get; }

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private string _countLabel = "";

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            Status = "Loading recommendations...";
            var dtos = await _api.GetRecommendationsAsync(200, ct);

            Rows.Clear();
            foreach (var d in dtos)
            {
                Rows.Add(new RecommendationRowVm
                {
                    Pool = $"{d.Token0}/{d.Token1} {FeeToPct(d.FeeTier)}",
                    RegimeLabel = RegimeToLabel(d.Regime),
                    Reinvest = d.ReinvestScore,
                    Reallocate = d.ReallocateScore,
                    Updated = d.CreatedUtc.LocalDateTime.ToString("g"),
                    Summary = d.Summary
                });
            }

            CountLabel = $"Loaded {Rows.Count} pools";
            Status = $"Updated {DateTimeOffset.Now:t}";
        }
        catch (Exception ex)
        {
            Status = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static string RegimeToLabel(int regime) => regime switch
    {
        0 => "Sideways",
        1 => "Trending",
        2 => "Volatile",
        _ => regime.ToString()
    };

    private static string FeeToPct(int feeTier)
    {
        // 500 => 0.05%, 3000 => 0.30%, 10000 => 1.00%
        var pct = feeTier / 10_000.0;
        return $"{pct:0.##}%";
    }
}
