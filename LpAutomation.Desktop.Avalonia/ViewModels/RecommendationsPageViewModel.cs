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

        DexOptions = new ObservableCollection<string> { "All DEXs" };
        ChainOptions = new ObservableCollection<string> { "All Chains" };
        RegimeOptions = new[] { "All Regimes", "Sideways", "Trending", "Volatile" };
        TimeframeOptions = new[] { "Any Time", "7 days", "14 days", "30 days", "90 days" };
        SortOptions = new[]
        {
            "Updated (newest)",
            "Reinvest (desc)",
            "Reallocate (desc)",
            "Pair (A-Z)"
        };

        SelectedDex = DexOptions[0];
        SelectedChain = ChainOptions[0];
        SelectedRegime = RegimeOptions[0];
        SelectedTimeframe = TimeframeOptions[1];
        SelectedSort = SortOptions[0];

        Status = "Ready";
        CountLabel = "Showing 0 pools";
    }

    public ObservableCollection<RecommendationRowVm> Rows { get; }
    public ObservableCollection<RecommendationRowVm> FilteredRows { get; }

    public ObservableCollection<string> DexOptions { get; }
    public ObservableCollection<string> ChainOptions { get; }

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _countLabel = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedDex = "All DEXs";
    [ObservableProperty] private string _selectedChain = "All Chains";
    [ObservableProperty] private string _selectedRegime = "All Regimes";
    [ObservableProperty] private string _selectedTimeframe = "7 days";
    [ObservableProperty] private string _selectedSort = "Updated (newest)";

    public IReadOnlyList<string> RegimeOptions { get; }
    public IReadOnlyList<string> TimeframeOptions { get; }
    public IReadOnlyList<string> SortOptions { get; }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedDexChanged(string value) => ApplyFilters();
    partial void OnSelectedChainChanged(string value) => ApplyFilters();
    partial void OnSelectedRegimeChanged(string value) => ApplyFilters();
    partial void OnSelectedTimeframeChanged(string value) => ApplyFilters();
    partial void OnSelectedSortChanged(string value) => ApplyFilters();

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            Status = "Loading discover pools...";
            var dtos = await _api.GetRecommendationsAsync(300, ct);

            Rows.Clear();
            foreach (var dto in dtos)
            {
                Rows.Add(new RecommendationRowVm
                {
                    Pool = $"{dto.Token0}/{dto.Token1}",
                    PoolAddress = dto.PoolAddress ?? "",
                    Dex = string.IsNullOrWhiteSpace(dto.Dex) ? "Unknown" : dto.Dex,
                    ChainId = dto.ChainId,
                    ChainLabel = ToChainLabel(dto.ChainId),
                    FeeLabel = FeeToPct(dto.FeeTier),
                    RegimeLabel = RegimeToLabel(dto.Regime),
                    Reinvest = dto.ReinvestScore,
                    Reallocate = dto.ReallocateScore,
                    UpdatedUtc = dto.CreatedUtc,
                    UpdatedLabel = dto.CreatedUtc.LocalDateTime.ToString("g"),
                    Summary = dto.Summary ?? ""
                });
            }

            RebuildOptionCollections();
            ApplyFilters();
            Status = $"Updated {DateTimeOffset.Now:t}";
        }
        catch (Exception ex)
        {
            Status = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchText = "";
        SelectedDex = DexOptions.FirstOrDefault() ?? "All DEXs";
        SelectedChain = ChainOptions.FirstOrDefault() ?? "All Chains";
        SelectedRegime = RegimeOptions[0];
        SelectedTimeframe = TimeframeOptions[1];
        SelectedSort = SortOptions[0];
        ApplyFilters();
        Status = "Discover filters reset.";
    }

    [RelayCommand]
    private void SimulatePool(string? poolAddress)
    {
        Status = string.IsNullOrWhiteSpace(poolAddress)
            ? "Pool address unavailable for this row."
            : $"Queued for Simulate: {poolAddress}";
    }

    private void RebuildOptionCollections()
    {
        var selectedDex = SelectedDex;
        var selectedChain = SelectedChain;

        var dexes = Rows
            .Select(r => r.Dex)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        DexOptions.Clear();
        DexOptions.Add("All DEXs");
        foreach (var dex in dexes)
            DexOptions.Add(dex);

        var chains = Rows
            .Select(r => r.ChainLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        ChainOptions.Clear();
        ChainOptions.Add("All Chains");
        foreach (var chain in chains)
            ChainOptions.Add(chain);

        SelectedDex = DexOptions.Contains(selectedDex) ? selectedDex : DexOptions[0];
        SelectedChain = ChainOptions.Contains(selectedChain) ? selectedChain : ChainOptions[0];
    }

    private void ApplyFilters()
    {
        IEnumerable<RecommendationRowVm> query = Rows;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            query = query.Where(r =>
                r.Pool.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.Summary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.PoolAddress.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedDex, "All DEXs", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.Dex, SelectedDex, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedChain, "All Chains", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.ChainLabel, SelectedChain, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedRegime, "All Regimes", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.RegimeLabel, SelectedRegime, StringComparison.OrdinalIgnoreCase));
        }

        if (TryGetTimeframeDays(out var days))
        {
            var since = DateTimeOffset.UtcNow.AddDays(-days);
            query = query.Where(r => r.UpdatedUtc >= since);
        }

        query = SelectedSort switch
        {
            "Reinvest (desc)" => query.OrderByDescending(r => r.Reinvest).ThenByDescending(r => r.UpdatedUtc),
            "Reallocate (desc)" => query.OrderByDescending(r => r.Reallocate).ThenByDescending(r => r.UpdatedUtc),
            "Pair (A-Z)" => query.OrderBy(r => r.Pool),
            _ => query.OrderByDescending(r => r.UpdatedUtc)
        };

        FilteredRows.Clear();
        foreach (var row in query)
            FilteredRows.Add(row);

        CountLabel = $"Showing {FilteredRows.Count} of {Rows.Count} pools";
    }

    private bool TryGetTimeframeDays(out int days)
    {
        days = 0;
        return SelectedTimeframe switch
        {
            "7 days" => (days = 7) > 0,
            "14 days" => (days = 14) > 0,
            "30 days" => (days = 30) > 0,
            "90 days" => (days = 90) > 0,
            _ => false
        };
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
        var pct = feeTier / 10_000.0;
        return $"{pct:0.##}%";
    }

    private static string ToChainLabel(int chainId) => chainId switch
    {
        1 => "Ethereum",
        10 => "Optimism",
        137 => "Polygon",
        8453 => "Base",
        _ => $"Chain {chainId}"
    };
}
