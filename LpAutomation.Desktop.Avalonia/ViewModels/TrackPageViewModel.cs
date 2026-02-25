using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Contracts.Config;
using LpAutomation.Contracts.PaperPositions;
using LpAutomation.Desktop.Avalonia.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class TrackPageViewModel : ObservableObject
{
    private readonly ConfigApiClient _configApi;
    private readonly RecommendationsApiClient _recsApi;
    private readonly PaperPositionsApiClient _paperApi;

    public TrackPageViewModel(
        ConfigApiClient configApi,
        RecommendationsApiClient recsApi,
        PaperPositionsApiClient paperApi)
    {
        _configApi = configApi;
        _recsApi = recsApi;
        _paperApi = paperApi;

        Rows = new ObservableCollection<TrackRowVm>();
        Status = "Ready";
        OwnerTagFilter = "eddie-dev";
    }

    public ObservableCollection<TrackRowVm> Rows { get; }

    [ObservableProperty] private string _ownerTagFilter = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private int _activePoolsCount;
    [ObservableProperty] private int _paperPositionsCount;
    [ObservableProperty] private int _matchedPoolsCount;
    [ObservableProperty] private string _lastUpdatedUtc = "-";

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            Status = "Loading track data...";

            var activePoolsTask = _configApi.GetActivePoolsAsync(ct);
            var recsTask = _recsApi.GetRecommendationsAsync(200, ct);
            var paperTask = _paperApi.ListAsync(OwnerTagFilter, 200, ct);

            await Task.WhenAll(activePoolsTask, recsTask, paperTask);

            var activePools = activePoolsTask.Result;
            var recs = recsTask.Result;
            var paper = paperTask.Result;

            ActivePoolsCount = activePools.Count;
            PaperPositionsCount = paper.Count;

            var paperByPool = paper
                .Where(x => !string.IsNullOrWhiteSpace(x.PoolAddress))
                .GroupBy(x => x.PoolAddress, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedUtc).First(), StringComparer.OrdinalIgnoreCase);

            Rows.Clear();

            foreach (var rec in recs)
            {
                paperByPool.TryGetValue(rec.PoolAddress ?? string.Empty, out var matched);

                Rows.Add(new TrackRowVm
                {
                    Pair = $"{rec.Token0}/{rec.Token1}",
                    PoolAddress = rec.PoolAddress ?? "(unresolved)",
                    FeeTier = rec.FeeTier,
                    Regime = RegimeToLabel(rec.Regime),
                    Reinvest = rec.ReinvestScore,
                    Reallocate = rec.ReallocateScore,
                    Status = matched is null ? "Observe" : "Simulating",
                    OwnerTag = matched?.OwnerTag ?? "-",
                    UpdatedUtc = rec.CreatedUtc.ToLocalTime().ToString("g")
                });
            }

            MatchedPoolsCount = Rows.Count(r => r.Status == "Simulating");
            LastUpdatedUtc = DateTimeOffset.UtcNow.ToString("u");
            Status = $"Loaded {Rows.Count} track row(s).";
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
}

public sealed class TrackRowVm
{
    public string Pair { get; init; } = "";
    public string PoolAddress { get; init; } = "";
    public int FeeTier { get; init; }
    public string Regime { get; init; } = "";
    public int Reinvest { get; init; }
    public int Reallocate { get; init; }
    public string Status { get; init; } = "";
    public string OwnerTag { get; init; } = "";
    public string UpdatedUtc { get; init; } = "";
}
