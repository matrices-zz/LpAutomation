using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Desktop.Avalonia.Services;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class RecommendationsPageViewModel : ObservableObject
{
    private static readonly string[] PlaceholderDexes =
    {
        "Uniswap V3", "Uniswap V4", "QuickSwap", "Pancake Swap", "SushiSwap",
        "Orca", "Raydium", "Velodrome", "Aerodrome", "HyperSwap",
        "ProjectX", "Shadow", "Blackhole", "Camelot"
    };

    private readonly RecommendationsApiClient _api;

    private static readonly string[] SupportedChainLabels =
    {
        "Ethereum", "Arbitrum", "Optimism", "Polygon", "Base", "BNB Chain", "Avalanche", "Celo"
    };

    private const string AllPreset = "All Opportunities";

    public RecommendationsPageViewModel(RecommendationsApiClient api)
    {
        _api = api;

        Rows = new ObservableCollection<RecommendationRowVm>();
        FilteredRows = new ObservableCollection<RecommendationRowVm>();

        DexFilterOptions = new ObservableCollection<DexFilterOptionVm>();
        ChainOptions = new ObservableCollection<string>(new[] { "All Chains" }.Concat(SupportedChainLabels));
        RegimeOptions = new[] { "All Regimes", "Sideways", "Trending", "Volatile" };
        TimeframeOptions = new[] { "Any Time", "7 days", "14 days", "30 days", "90 days" };
        SortOptions = new[]
        {
            "Updated (newest)",
            "Decision Confidence (desc)",
            "Reinvest (desc)",
            "Reallocate (desc)",
            "Pair (A-Z)"
        };

        PresetOptions = new[]
        {
            AllPreset,
            "High Reinvest (>=70)",
            "High Reallocate (>=70)",
            "Trending + Low Fee",
            "Recently Updated (24h)"
        };

        SelectedChain = ChainOptions[0];
        SelectedRegime = RegimeOptions[0];
        SelectedTimeframe = TimeframeOptions[1];
        SelectedSort = SortOptions[0];
        SelectedPreset = PresetOptions[0];

        foreach (var dex in PlaceholderDexes)
        {
            var option = new DexFilterOptionVm(dex, true);
            option.PropertyChanged += OnDexOptionChanged;
            DexFilterOptions.Add(option);
        }

        UpdateSelectedDexSummary();

        Status = "Ready";
        CountLabel = "Showing 0 pools";
    }

    public ObservableCollection<RecommendationRowVm> Rows { get; }
    public ObservableCollection<RecommendationRowVm> FilteredRows { get; }

    public ObservableCollection<DexFilterOptionVm> DexFilterOptions { get; }
    public ObservableCollection<string> ChainOptions { get; }

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _countLabel = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedChain = "All Chains";
    [ObservableProperty] private string _selectedRegime = "All Regimes";
    [ObservableProperty] private string _selectedTimeframe = "7 days";
    [ObservableProperty] private string _selectedSort = "Updated (newest)";
    [ObservableProperty] private bool _isDexSelectorOpen;
    [ObservableProperty] private string _selectedDexSummary = "All DEXs";
    [ObservableProperty] private string _selectedPreset = AllPreset;

    public IReadOnlyList<string> RegimeOptions { get; }
    public IReadOnlyList<string> TimeframeOptions { get; }
    public IReadOnlyList<string> SortOptions { get; }
    public IReadOnlyList<string> PresetOptions { get; }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedChainChanged(string value) => ApplyFilters();
    partial void OnSelectedRegimeChanged(string value) => ApplyFilters();
    partial void OnSelectedTimeframeChanged(string value) => ApplyFilters();
    partial void OnSelectedSortChanged(string value) => ApplyFilters();
    partial void OnSelectedPresetChanged(string value) => ApplyFilters();

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
                var heat = TryGetBlendedHeat(dto.DetailsJson);
                var volatilityM15 = TryGetVolatilityM15(dto.DetailsJson);
                var marketScore = ComputeOpportunityScore(dto.ReinvestScore, dto.ReallocateScore, heat, volatilityM15);
                var (decisionLabel, decisionConfidence, decisionBrush) = ComputeDecision(dto.ReinvestScore, dto.ReallocateScore);

                var decisionTooltip = $"Decision confidence = |Reinvest - Reallocate| = {decisionConfidence}. Higher means clearer directional bias.";
                var heatTooltip = $"Market Heat (0-100): blended risk temperature from multi-timeframe signals. Lower is cooler, higher is hotter/riskier. Current: {heat}.";
                var opportunityTooltip = BuildOpportunityTooltip(marketScore, decisionConfidence, Math.Max(dto.ReinvestScore, dto.ReallocateScore), heat, volatilityM15);
                var reinvestTooltip = BuildActionScoreTooltip("Reinvest", dto.ReinvestScore, heat, volatilityM15);
                var reallocateTooltip = BuildActionScoreTooltip("Reallocate", dto.ReallocateScore, heat, volatilityM15);

                Rows.Add(new RecommendationRowVm
                {
                    Pool = $"{dto.Token0}/{dto.Token1}",
                    PoolAddress = dto.PoolAddress ?? "",
                    Dex = string.IsNullOrWhiteSpace(dto.Dex) ? "Unknown" : dto.Dex,
                    ChainId = dto.ChainId,
                    ChainLabel = ToChainLabel(dto.ChainId),
                    FeeTier = dto.FeeTier,
                    FeeLabel = FeeToPct(dto.FeeTier),
                    RegimeLabel = RegimeToLabel(dto.Regime),
                    Reinvest = dto.ReinvestScore,
                    ReinvestBrush = ReinvestScoreToBrush(dto.ReinvestScore),
                    ReinvestTooltip = reinvestTooltip,
                    Reallocate = dto.ReallocateScore,
                    ReallocateBrush = ReallocateScoreToBrush(dto.ReallocateScore),
                    ReallocateTooltip = reallocateTooltip,
                    DecisionLabel = decisionLabel,
                    DecisionConfidence = decisionConfidence,
                    DecisionBrush = decisionBrush,
                    DecisionConfidenceTooltip = decisionTooltip,
                    MarketHeat = heat,
                    MarketHeatTooltip = heatTooltip,
                    OpportunityScore = marketScore,
                    OpportunityTooltip = opportunityTooltip,
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
        foreach (var dex in DexFilterOptions)
            dex.IsSelected = true;

        SelectedChain = ChainOptions.FirstOrDefault() ?? "All Chains";
        SelectedRegime = RegimeOptions[0];
        SelectedTimeframe = TimeframeOptions[1];
        SelectedSort = SortOptions[0];
        SelectedPreset = PresetOptions[0];
        ApplyFilters();
        UpdateSelectedDexSummary();
        Status = "Discover filters reset.";
    }


    [RelayCommand]
    private void ResetSort()
    {
        SelectedSort = SortOptions[0];
    }

    [RelayCommand]
    private void SetPreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
            return;

        SelectedPreset = preset;
    }

    [RelayCommand]
    private void SimulatePool(string? poolAddress)
    {
        Status = string.IsNullOrWhiteSpace(poolAddress)
            ? "Pool address unavailable for this row."
            : $"Queued for Simulate: {poolAddress}";
    }


    [RelayCommand]
    private void ToggleDexSelector()
    {
        IsDexSelectorOpen = !IsDexSelectorOpen;
    }

    [RelayCommand]
    private void SelectAllDexes()
    {
        foreach (var dex in DexFilterOptions)
            dex.IsSelected = true;

        ApplyFilters();
        UpdateSelectedDexSummary();
    }

    [RelayCommand]
    private void ClearDexes()
    {
        foreach (var dex in DexFilterOptions)
            dex.IsSelected = false;

        ApplyFilters();
        UpdateSelectedDexSummary();
    }

    private void RebuildOptionCollections()
    {
        var selectedChain = SelectedChain;

        var discoveredDexes = Rows
            .Select(r => r.Dex)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var existing = DexFilterOptions.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var dex in PlaceholderDexes.Concat(discoveredDexes).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (existing.ContainsKey(dex))
                continue;

            var option = new DexFilterOptionVm(dex, true);
            option.PropertyChanged += OnDexOptionChanged;
            DexFilterOptions.Add(option);
        }

        var chains = SupportedChainLabels
            .Concat(Rows.Select(r => r.ChainLabel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        ChainOptions.Clear();
        ChainOptions.Add("All Chains");
        foreach (var chain in chains)
            ChainOptions.Add(chain);

        SelectedChain = ChainOptions.Contains(selectedChain) ? selectedChain : ChainOptions[0];
        UpdateSelectedDexSummary();
    }


    private void OnDexOptionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(DexFilterOptionVm.IsSelected), StringComparison.Ordinal))
            return;

        ApplyFilters();
        UpdateSelectedDexSummary();
    }

    private void UpdateSelectedDexSummary()
    {
        var selected = DexFilterOptions.Where(x => x.IsSelected).Select(x => x.Name).ToList();

        SelectedDexSummary = selected.Count switch
        {
            0 => "No DEXs selected",
            1 => selected[0],
            2 => $"{selected[0]}, {selected[1]}",
            _ => $"{selected[0]}, {selected[1]} +{selected.Count - 2}"
        };
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

        var selectedDexes = DexFilterOptions.Where(x => x.IsSelected).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedDexes.Count > 0)
        {
            query = query.Where(r => selectedDexes.Contains(r.Dex));
        }
        else
        {
            query = Enumerable.Empty<RecommendationRowVm>();
        }

        if (!string.Equals(SelectedChain, "All Chains", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.ChainLabel, SelectedChain, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedRegime, "All Regimes", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.RegimeLabel, SelectedRegime, StringComparison.OrdinalIgnoreCase));
        }

        query = SelectedPreset switch
        {
            "High Reinvest (>=70)" => query.Where(r => r.Reinvest >= 70),
            "High Reallocate (>=70)" => query.Where(r => r.Reallocate >= 70),
            "Trending + Low Fee" => query.Where(r =>
                string.Equals(r.RegimeLabel, "Trending", StringComparison.OrdinalIgnoreCase) && r.FeeTier <= 3000),
            "Recently Updated (24h)" => query.Where(r => r.UpdatedUtc >= DateTimeOffset.UtcNow.AddHours(-24)),
            _ => query
        };

        if (TryGetTimeframeDays(out var days))
        {
            var since = DateTimeOffset.UtcNow.AddDays(-days);
            query = query.Where(r => r.UpdatedUtc >= since);
        }

        query = SelectedSort switch
        {
            "Decision Confidence (desc)" => query.OrderByDescending(r => r.DecisionConfidence).ThenByDescending(r => r.OpportunityScore).ThenByDescending(r => r.UpdatedUtc),
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
        42161 => "Arbitrum",
        10 => "Optimism",
        137 => "Polygon",
        8453 => "Base",
        56 => "BNB Chain",
        43114 => "Avalanche",
        42220 => "Celo",
        _ => $"Chain {chainId}"
    };




    private static (string Label, int Confidence, string Brush) ComputeDecision(int reinvest, int reallocate)
    {
        var delta = reinvest - reallocate;
        var confidence = Math.Abs(delta);

        if (confidence < 10)
            return ("Hold", confidence, "#64748B");

        return delta > 0
            ? ("Reinvest", confidence, "#16A34A")
            : ("Reallocate", confidence, "#DC2626");
    }

    private static int ComputeOpportunityScore(int reinvest, int reallocate, int heat, double volatility)
    {
        var confidence = Math.Abs(reinvest - reallocate);

        const double confidenceWeight = 0.45;
        const double actionWeight = 0.30;
        const double heatWeight = 0.15;
        const double volatilityWeight = 0.10;

        var dominantAction = Math.Max(reinvest, reallocate);
        var heatPenalty = 100 - Math.Clamp(heat, 0, 100);
        var volPenalty = 100 - Math.Clamp((int)Math.Round(volatility * 100), 0, 100);

        var raw = (confidence * confidenceWeight) +
                  (dominantAction * actionWeight) +
                  (heatPenalty * heatWeight) +
                  (volPenalty * volatilityWeight);

        return Math.Clamp((int)Math.Round(raw), 0, 100);
    }


    private static string BuildOpportunityTooltip(int score, int confidence, int dominantAction, int heat, double volatility)
    {
        var heatPenalty = 100 - Math.Clamp(heat, 0, 100);
        var volPenalty = 100 - Math.Clamp((int)Math.Round(volatility * 100), 0, 100);

        return $"Opportunity score {score}/100\n" +
               $"Weights: confidence 45%, action 30%, heat 15%, volatility 10%\n" +
               $"Inputs: confidence={confidence}, dominantAction={dominantAction}, heatPenalty={heatPenalty}, volPenalty={volPenalty}";
    }


    private static string BuildActionScoreTooltip(string actionName, int score, int heat, double volatility)
    {
        var volatilityBps = Math.Round(volatility * 10_000, MidpointRounding.AwayFromZero);

        return $"{actionName} score: {score}/100\n" +
               "Derived from weighted market signals (5m, 15m, 1h, 6h, 24h):\n" +
               "• Price-action trend and momentum\n" +
               "• Relative volume/participation\n" +
               $"• Heat/risk context (current heat: {heat})\n" +
               $"• Intraday volatility context (15m: {volatilityBps} bps)\n" +
               "Higher score means stronger signal for this action.";
    }

    private static int TryGetBlendedHeat(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return 50;

        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            if (doc.RootElement.TryGetProperty("heat", out var heatEl) &&
                heatEl.TryGetProperty("blended", out var blendedEl) &&
                blendedEl.TryGetInt32(out var blended))
            {
                return Math.Clamp(blended, 0, 100);
            }
        }
        catch
        {
            // ignore malformed details json
        }

        return 50;
    }

    private static double TryGetVolatilityM15(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return 0.5;

        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            if (doc.RootElement.TryGetProperty("vol", out var volEl) &&
                volEl.TryGetProperty("m15", out var m15El) &&
                m15El.TryGetDouble(out var vol))
            {
                return Math.Clamp(vol, 0d, 1d);
            }
        }
        catch
        {
            // ignore malformed details json
        }

        return 0.5;
    }

    private static string ReinvestScoreToBrush(int score) => score switch
    {
        >= 75 => "#16A34A",
        >= 50 => "#65A30D",
        >= 30 => "#D97706",
        _ => "#DC2626"
    };

    private static string ReallocateScoreToBrush(int score) => score switch
    {
        >= 75 => "#DC2626",
        >= 50 => "#EA580C",
        >= 30 => "#65A30D",
        _ => "#16A34A"
    };
}
