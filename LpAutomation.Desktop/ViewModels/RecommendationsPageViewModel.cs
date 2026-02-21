using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace LpAutomation.Desktop.ViewModels;

public sealed partial class RecommendationsPageViewModel : ObservableObject
{
    private readonly RecommendationsApiClient _api;

    // Last-seen heat by pool (for arrow + delta)
    private readonly Dictionary<string, int> _lastHeatByPool = new(StringComparer.OrdinalIgnoreCase);

    public RecommendationsPageViewModel(RecommendationsApiClient api)
    {
        _api = api;

        Rows = new ObservableCollection<PoolRecommendationRowViewModel>();
        FilteredRows = new ObservableCollection<PoolRecommendationRowViewModel>();

        Status = "Ready";
        CountLabel = "Loaded 0 pools";

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public ObservableCollection<PoolRecommendationRowViewModel> Rows { get; }
    public ObservableCollection<PoolRecommendationRowViewModel> FilteredRows { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isCompactMode;

    [ObservableProperty]
    private string _countLabel = "";

    public IReadOnlyList<string> RegimeFilterOptions { get; } =
    new[] { "All", "Sideways", "Trending", "Volatile" };

    [ObservableProperty]
    private string _selectedRegimeFilter = "All";

    public IReadOnlyList<string> ActionFilterOptions { get; } =
        new[] { "All", "Hold", "Reinvest", "Reduce" };

    [ObservableProperty]
    private string _selectedActionFilter = "All";

    public IReadOnlyList<string> SortOptions { get; } =
        new[] { "Heat (desc)", "Confidence (desc)", "Reallocate (desc)", "Reinvest (desc)", "Updated (newest)" };

    [ObservableProperty]
    private string _selectedSort = "Heat (desc)";

    public IReadOnlyList<string> TimeframeOptions { get; } =
        new[] { "Auto" }; // placeholder for now (server-driven later)

    [ObservableProperty]
    private string _selectedTimeframe = "Auto";

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedRegimeFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedActionFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedSortChanged(string value) => ApplyFilters();
    partial void OnSelectedTimeframeChanged(string value) => ApplyFilters();

    partial void OnIsCompactModeChanged(bool value)
    {
        foreach (var r in Rows)
            r.IsCompact = value;

        ApplyFilters();
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            Status = "Loading recommendations...";

            var dtos = await _api.GetRecommendationsAsync(take: 200, ct);

            // Snapshot previous heat map so deltas are stable during this refresh.
            var prevHeat = new Dictionary<string, int>(_lastHeatByPool, StringComparer.OrdinalIgnoreCase);

            Rows.Clear();

            foreach (var dto in dtos)
            {
                var row = BuildRow(dto, prevHeat);
                Rows.Add(row);
            }

            ApplyFilters();

            CountLabel = $"Loaded {FilteredRows.Count} pools";
            Status = $"Updated {DateTimeOffset.Now:t}";
        }
        catch (Exception ex)
        {
            Status = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private PoolRecommendationRowViewModel BuildRow(RecommendationDto dto, IReadOnlyDictionary<string, int> prevHeat)
    {
        var row = PoolRecommendationRowViewModel.FromDto(dto);
        row.IsCompact = IsCompactMode;

        // Make the pool header prettier than raw feeTier
        row.PoolName = $"{row.Token0}/{row.Token1} {FeeTierToPct(row.FeeTier)}";
        row.PoolMeta = $"Ethereum • {row.Dex} • {row.ProtocolVersion}";

        // Regime
        row.RegimeLabel = dto.Regime switch
        {
            0 => "Sideways",
            1 => "Trending",
            2 => "Volatile",
            _ => dto.Regime.ToString()
        };
        row.RegimeBrush = row.RegimeLabel switch
        {
            "Trending" => MakeBrush(180, 120, 220),
            "Volatile" => MakeBrush(240, 140, 120),
            _ => MakeBrush(200, 200, 200)
        };

        // Scores
        row.ReinvestScore = dto.ReinvestScore;
        row.ReallocateScore = dto.ReallocateScore;
        row.ReinvestScoreLabel = $"{dto.ReinvestScore}/100";
        row.ReallocateScoreLabel = $"{dto.ReallocateScore}/100";

        // Action
        row.ActionLabel = DecideAction(dto.ReinvestScore, dto.ReallocateScore);
        row.ActionBrush = row.ActionLabel switch
        {
            "Reinvest" => MakeBrush(170, 255, 90),
            "Reduce" => MakeBrush(255, 220, 90),
            _ => MakeBrush(210, 210, 210)
        };

        // Sentiment (placeholder)
        row.SentimentLabel = "Neutral";
        row.SentimentDetail = "";

        // Heat delta + arrow (risk semantics: up = hotter)
        var poolKey = !string.IsNullOrWhiteSpace(row.PoolAddress)
            ? row.PoolAddress.Trim().ToLowerInvariant()
            : $"{row.Token0}/{row.Token1}/{row.FeeTier}".ToLowerInvariant();

        prevHeat.TryGetValue(poolKey, out var prev);
        var delta = row.Heat - prev;

        _lastHeatByPool[poolKey] = row.Heat;

        row.HeatArrowGlyph = delta > 0 ? "▲" : delta < 0 ? "▼" : "→";
        row.HeatDeltaDisplay = delta == 0 ? "" : (delta > 0 ? $"+{delta}" : delta.ToString());

        // Confidence (simple heuristic; replace later with “institutional-grade” model)
        row.Confidence = ClampInt(100 - Math.Abs(row.Heat - 50) * 2, 0, 100);
        row.ConfidenceLabel = $"{row.Confidence}%";

        // Updated
        row.UpdatedAgo = ToAgo(row.CreatedUtc, DateTimeOffset.UtcNow);
        row.SnapshotTimeDisplay = row.CreatedUtc.ToLocalTime().ToString("g");

        return row;
    }

    private void ApplyFilters()
    {
        FilteredRows.Clear();

        var q = (SearchText ?? string.Empty).Trim();
        if (q.Length > 0)
            q = q.ToLowerInvariant();

        IEnumerable<PoolRecommendationRowViewModel> query = Rows;

        // Regime filter
        if (!string.Equals(SelectedRegimeFilter, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(r => string.Equals(r.RegimeLabel, SelectedRegimeFilter, StringComparison.OrdinalIgnoreCase));

        // Action filter
        if (!string.Equals(SelectedActionFilter, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(r => string.Equals(r.ActionLabel, SelectedActionFilter, StringComparison.OrdinalIgnoreCase));

        // Search
        if (q.Length > 0)
        {
            query = query.Where(r =>
            {
                var hay = $"{r.PoolName} {r.PoolMeta} {r.PoolAddressShort} {r.RegimeLabel} {r.ActionLabel}".ToLowerInvariant();
                return hay.Contains(q);
            });
        }

        // Sort
        query = SelectedSort switch
        {
            "Confidence (desc)" => query.OrderByDescending(r => r.Confidence).ThenByDescending(r => r.Heat),
            "Reallocate (desc)" => query.OrderByDescending(r => r.ReallocateScore).ThenByDescending(r => r.Heat),
            "Reinvest (desc)" => query.OrderByDescending(r => r.ReinvestScore).ThenByDescending(r => r.Heat),
            "Updated (newest)" => query.OrderByDescending(r => r.CreatedUtc),
            _ => query.OrderByDescending(r => r.Heat).ThenByDescending(r => r.Confidence), // Heat (desc)
        };

        foreach (var r in Rows)
        {
            if (q.Length == 0)
            {
                FilteredRows.Add(r);
                continue;
            }

            var hay = $"{r.PoolName} {r.PoolMeta} {r.PoolAddressShort} {r.RegimeLabel} {r.ActionLabel}".ToLowerInvariant();
            if (hay.Contains(q))
                FilteredRows.Add(r);
        }

        CountLabel = $"Loaded {FilteredRows.Count} pools";
    }

    private static string DecideAction(int reinvest, int reallocate)
    {
        if (reallocate >= 70 && reinvest <= 40) return "Reduce";
        if (reinvest >= 70 && reallocate <= 40) return "Reinvest";
        return "Hold";
    }

    private static string FeeTierToPct(int feeTier)
        => feeTier switch
        {
            500 => "0.05%",
            3000 => "0.30%",
            10000 => "1.00%",
            _ => $"{feeTier}"
        };

    private static int ClampInt(int v, int lo, int hi)
    {
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }

    private static string ToAgo(DateTimeOffset utc, DateTimeOffset nowUtc)
    {
        var age = nowUtc - utc;
        if (age < TimeSpan.FromMinutes(1)) return "just now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes}m ago";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }

    private static Brush MakeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}