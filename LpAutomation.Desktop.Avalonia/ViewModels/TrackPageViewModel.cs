using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Contracts.Config;
using LpAutomation.Contracts.PaperPositions;
using LpAutomation.Desktop.Avalonia.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class TrackPageViewModel : ObservableObject
{
    private const int MaxTrackedPools = 6;
    private readonly ConfigApiClient _configApi;
    private readonly RecommendationsApiClient _recsApi;
    private readonly PaperPositionsApiClient _paperApi;
    private CancellationTokenSource? _autoRefreshCts;

    public TrackPageViewModel(
        ConfigApiClient configApi,
        RecommendationsApiClient recsApi,
        PaperPositionsApiClient paperApi)
    {
        _configApi = configApi;
        _recsApi = recsApi;
        _paperApi = paperApi;

        Rows = new ObservableCollection<TrackRowVm>();
        OwnerTagFilter = "eddie-dev";
        RefreshIntervalSeconds = 30;
        StatusMessage = "Ready";
        LastUpdatedUtc = "-";
    }

    public ObservableCollection<TrackRowVm> Rows { get; }

    public string AutoRefreshButtonLabel => AutoRefreshEnabled ? "Stop Auto" : "Start Auto";

    [ObservableProperty] private string _ownerTagFilter = "";
    [ObservableProperty] private string _chainFilterText = "";
    [ObservableProperty] private int _refreshIntervalSeconds;
    [ObservableProperty] private bool _autoRefreshEnabled;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private int _activePoolsCount;
    [ObservableProperty] private int _paperPositionsCount;
    [ObservableProperty] private double _inRangePct;
    [ObservableProperty] private decimal _estimatedNetExposureUsd;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private string _lastUpdatedUtc = "";

    partial void OnAutoRefreshEnabledChanged(bool value) => OnPropertyChanged(nameof(AutoRefreshButtonLabel));

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            StatusMessage = "Loading tracked LP positions...";

            var activePoolsTask = _configApi.GetActivePoolsAsync(ct);
            var recsTask = _recsApi.GetRecommendationsAsync(200, ct);
            var paperTask = _paperApi.ListAsync(OwnerTagFilter, 200, ct);

            await Task.WhenAll(activePoolsTask, recsTask, paperTask);

            var activePools = activePoolsTask.Result;
            var recs = recsTask.Result;
            var paper = paperTask.Result;

            if (TryGetChainFilter(out var chainId))
            {
                activePools = activePools.Where(x => x.ChainId == chainId).ToList();
                recs = recs.Where(x => x.ChainId == chainId).ToList();
                paper = paper.Where(x => x.ChainId == chainId).ToList();
            }

            ActivePoolsCount = activePools.Count;
            PaperPositionsCount = paper.Count;

            var recByAddress = recs
                .Where(x => !string.IsNullOrWhiteSpace(x.PoolAddress))
                .GroupBy(x => x.PoolAddress!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedUtc).First(), StringComparer.OrdinalIgnoreCase);

            var recBySignature = recs
                .GroupBy(ToPoolSignature)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedUtc).First(), StringComparer.OrdinalIgnoreCase);

            var trackedPositions = paper
                .OrderByDescending(x => x.UpdatedUtc)
                .Take(MaxTrackedPools)
                .ToList();

            Rows.Clear();

            foreach (var position in trackedPositions)
            {
                recByAddress.TryGetValue(position.PoolAddress ?? string.Empty, out var matchedRec);
                matchedRec ??= recBySignature.GetValueOrDefault(ToPoolSignature(position));

                var row = BuildRow(position, matchedRec, activePools);
                Rows.Add(row);
            }

            var inRangeCount = Rows.Count(r => string.Equals(r.Status, "Healthy", StringComparison.OrdinalIgnoreCase));
            InRangePct = Rows.Count == 0 ? 0 : (100.0 * inRangeCount / Rows.Count);
            WarningCount = Rows.Count(r => !string.Equals(r.Status, "Healthy", StringComparison.OrdinalIgnoreCase));
            EstimatedNetExposureUsd = Rows.Sum(x => x.LiquidityUsd ?? 0m);
            LastUpdatedUtc = DateTimeOffset.UtcNow.ToString("u");
            StatusMessage = Rows.Count == 0
                ? "No tracked LP positions found for this owner/chain filter."
                : $"Loaded {Rows.Count} tracked position(s) (showing up to {MaxTrackedPools}).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Refresh cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenPaperPosition(Guid? positionId)
    {
        StatusMessage = positionId is null
            ? "No paper position linked to this row."
            : $"Linked paper position: {positionId}";
    }

    [RelayCommand]
    private void OpenRecommendation(string? poolAddress)
    {
        StatusMessage = string.IsNullOrWhiteSpace(poolAddress)
            ? "Recommendation has no pool address."
            : $"Recommendation pool: {poolAddress}";
    }

    [RelayCommand]
    private async Task ToggleAutoRefreshAsync()
    {
        AutoRefreshEnabled = !AutoRefreshEnabled;
        StopAutoRefreshLoop();

        if (!AutoRefreshEnabled)
        {
            StatusMessage = "Auto refresh disabled.";
            return;
        }

        if (RefreshIntervalSeconds < 5)
        {
            RefreshIntervalSeconds = 5;
            StatusMessage = "Refresh interval adjusted to 5s minimum.";
        }

        _autoRefreshCts = new CancellationTokenSource();
        var token = _autoRefreshCts.Token;

        StatusMessage = $"Auto refresh enabled ({RefreshIntervalSeconds}s).";
        await RefreshAsync(token);
        _ = RunAutoRefreshLoopAsync(token);
    }

    private async Task RunAutoRefreshLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(RefreshIntervalSeconds), token);
                await RefreshAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Auto refresh stopped.";
        }
    }

    private void StopAutoRefreshLoop()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _autoRefreshCts = null;
    }

    private bool TryGetChainFilter(out long chainId)
    {
        chainId = 0;
        if (string.IsNullOrWhiteSpace(ChainFilterText))
            return false;

        if (long.TryParse(ChainFilterText.Trim(), out var parsed))
        {
            chainId = parsed;
            return true;
        }

        StatusMessage = "Chain filter ignored: expected integer chain id.";
        return false;
    }

    private static TrackRowVm BuildRow(PaperPositionDto position, RecommendationDto? recommendation, IReadOnlyList<ActivePoolDto> activePools)
    {
        var hasPoolInConfig = activePools.Any(x => IsConfigMatch(x, position));

        var status = GetStatus(position, recommendation, hasPoolInConfig);

        return new TrackRowVm
        {
            Pair = $"{position.Token0Symbol}/{position.Token1Symbol}",
            PoolAddress = position.PoolAddress ?? "(unresolved)",
            PoolAddressShort = ShortAddress(position.PoolAddress),
            FeeTier = position.FeeTier,
            Regime = recommendation is null ? "-" : RegimeToLabel(recommendation.Regime),
            RecommendationAction = recommendation is null
                ? "Hold"
                : recommendation.ReallocateScore >= recommendation.ReinvestScore ? "Reallocate" : "Reinvest",
            PaperPositionId = position.PositionId,
            LiquidityUsd = position.LiquidityNotionalUsd,
            EstimatedExposurePct = recommendation is null ? null : Math.Clamp(50 + (recommendation.ReinvestScore - recommendation.ReallocateScore) / 2.0, 0, 100),
            Status = status,
            Diagnostics = BuildDiagnostics(position, recommendation, hasPoolInConfig),
            UpdatedUtc = position.UpdatedUtc.ToLocalTime().ToString("g")
        };
    }

    private static bool IsConfigMatch(ActivePoolDto activePool, PaperPositionDto position)
    {
        return activePool.ChainId == position.ChainId &&
               activePool.FeeTier == position.FeeTier &&
               string.Equals(activePool.Token0, position.Token0Symbol, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(activePool.Token1, position.Token1Symbol, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDiagnostics(PaperPositionDto position, RecommendationDto? recommendation, bool hasPoolInConfig)
    {
        var tags = new List<string>
        {
            "owner-book",
            hasPoolInConfig ? "active-config" : "not-in-active-config",
            position.Enabled ? "paper:enabled" : "paper:disabled",
            recommendation is null
                ? "signal:none"
                : recommendation.ReallocateScore >= 70 ? "signal:high-reallocate" : "signal:normal"
        };

        return string.Join(" | ", tags);
    }

    private static string GetStatus(PaperPositionDto position, RecommendationDto? recommendation, bool hasPoolInConfig)
    {
        if (!position.Enabled)
            return "Stale";

        if (!hasPoolInConfig)
            return "Warn";

        if (recommendation?.ReallocateScore >= 70)
            return "OutOfRange";

        if (position.LiquidityNotionalUsd <= 0)
            return "Warn";

        return "Healthy";
    }

    private static string ShortAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "(unresolved)";

        var trimmed = value.Trim();
        return trimmed.Length <= 14
            ? trimmed
            : $"{trimmed[..6]}...{trimmed[^4..]}";
    }

    private static string ToPoolSignature(PaperPositionDto row)
        => $"{row.ChainId}|{row.Token0Symbol}|{row.Token1Symbol}|{row.FeeTier}".ToLowerInvariant();

    private static string ToPoolSignature(RecommendationDto row)
        => $"{row.ChainId}|{row.Token0}|{row.Token1}|{row.FeeTier}".ToLowerInvariant();

    private static string RegimeToLabel(int regime) => regime switch
    {
        0 => "Sideways",
        1 => "Trending",
        2 => "Volatile",
        _ => regime.ToString()
    };
}

public sealed class TrackRowVm
{
    public string Pair { get; init; } = "";
    public string PoolAddress { get; init; } = "";
    public string PoolAddressShort { get; init; } = "";
    public int FeeTier { get; init; }
    public string Regime { get; init; } = "";
    public string RecommendationAction { get; init; } = "";
    public Guid? PaperPositionId { get; init; }
    public decimal? LiquidityUsd { get; init; }
    public double? EstimatedExposurePct { get; init; }
    public string Status { get; init; } = "";
    public string Diagnostics { get; init; } = "";
    public string UpdatedUtc { get; init; } = "";
}
