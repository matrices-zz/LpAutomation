using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Desktop.Avalonia.Services;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class RecommendationsPageViewModel : ObservableObject
{
    private readonly RecommendationsApiClient _api;

    public RecommendationsPageViewModel(RecommendationsApiClient api)
    {
        _api = api;

        Rows = new ObservableCollection<RecommendationRowVm>();
        FilteredRows = new ObservableCollection<RecommendationRowVm>();

        Status = "Ready";
        CountLabel = "Loaded 0 pools";

        RegimeOptions = new[] { "All", "Sideways", "Trending", "Volatile" };
        SortOptions = new[] { "Updated (newest)", "Reinvest (desc)", "Reallocate (desc)" };

        SelectedRegime = "All";
        SelectedSort = "Updated (newest)";
    }

    public ObservableCollection<RecommendationRowVm> Rows { get; }
    public ObservableCollection<RecommendationRowVm> FilteredRows { get; }

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _countLabel = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedRegime = "All";
    [ObservableProperty] private string _selectedSort = "Updated (newest)";

    public IReadOnlyList<string> RegimeOptions { get; }
    public IReadOnlyList<string> SortOptions { get; }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedRegimeChanged(string value) => ApplyFilters();
    partial void OnSelectedSortChanged(string value) => ApplyFilters();

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
                    Summary = d.Summary ?? ""
                });
            }

            ApplyFilters();
            Status = $"Updated {DateTimeOffset.Now:t}";
        }
        catch (Exception ex)
        {
            Status = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<RecommendationRowVm> q = Rows;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim();
            q = q.Where(x =>
                x.Pool.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.Summary.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedRegime, "All", StringComparison.OrdinalIgnoreCase))
        {
            q = q.Where(x => string.Equals(x.RegimeLabel, SelectedRegime, StringComparison.OrdinalIgnoreCase));
        }

        q = SelectedSort switch
        {
            "Reinvest (desc)" => q.OrderByDescending(x => x.Reinvest),
            "Reallocate (desc)" => q.OrderByDescending(x => x.Reallocate),
            _ => q.OrderByDescending(x => ParseUpdated(x.Updated))
        };

        FilteredRows.Clear();
        foreach (var row in q)
            FilteredRows.Add(row);

        CountLabel = $"Loaded {FilteredRows.Count} pools";
    }

    private static DateTime ParseUpdated(string s)
        => DateTime.TryParse(s, out var dt) ? dt : DateTime.MinValue;

    private static string RegimeToLabel(int regime) => regime switch
    {
        0 => "Sideways",
        1 => "Trending",
        2 => "Volatile",
        _ => regime.ToString()
    };

    private static string FeeToPct(int feeTier)
    {
        var pct = feeTier / 10_000.0;
        return $"{pct:0.##}%";
    }
}
