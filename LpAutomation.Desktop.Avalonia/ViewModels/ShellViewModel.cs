using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Desktop.Avalonia.Services;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly ConfigApiClient _configApi;
    private readonly RecommendationsApiClient _recsApi;
    private readonly IFileDialogService _files;

    private readonly TrackPageViewModel _trackPage;
    private readonly RecommendationsPageViewModel _recommendationsPage;
    private readonly PaperPositionsPageViewModel _paperPositionsPage;
    private readonly SettingsPageViewModel _settingsPage;

    [ObservableProperty]
    private string _title = "LP Automation — Avalonia";

    [ObservableProperty]
    private string _subtitle = "Desktop MVP (Phase 2.1)";

    [ObservableProperty]
    private string _currentPageTitle = "Discover";

    [ObservableProperty]
    private object? _currentPage;

    // Global shell health/status (show on all tabs)
    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusKind = "Neutral"; // Neutral | Running | Success | Error

    [ObservableProperty]
    private int _lastPayloadChars;

    [ObservableProperty]
    private double _lastElapsedMs;

    [ObservableProperty]
    private int _apiCallCount;

    [ObservableProperty]
    private int _apiErrorCount;

    [ObservableProperty]
    private string _lastUpdatedUtc = "-";

    [ObservableProperty]
    private string _previewTitle = "API Preview (truncated)";

    [ObservableProperty]
    private string _lastApiPreview = "No data yet.";

    // Optional: bind top-right button text
    [ObservableProperty]
    private string _testApiButtonText = "Check Connection";

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public ShellViewModel(
        ConfigApiClient configApi,
        RecommendationsApiClient recsApi,
        IFileDialogService files,
        TrackPageViewModel trackPage,
        RecommendationsPageViewModel recommendationsPage,
        PaperPositionsPageViewModel paperPositionsPage,
        SettingsPageViewModel settingsPage)
    {
        _configApi = configApi;
        _recsApi = recsApi;
        _files = files;
        _trackPage = trackPage;
        _recommendationsPage = recommendationsPage;
        _paperPositionsPage = paperPositionsPage;
        _settingsPage = settingsPage;

        // Phase 1 navigation alignment (Track / Discover / Simulate)
        NavItems.Add(new NavItem("Track", "dashboard", () => _trackPage));
        NavItems.Add(new NavItem("Discover", "star", () => _recommendationsPage));
        NavItems.Add(new NavItem("Simulate", "briefcase", () => _paperPositionsPage));
        NavItems.Add(new NavItem("Settings", "settings", () => _settingsPage));


        // Default page
        var first = NavItems[1]; // Discover
        CurrentPage = first.Create();
        CurrentPageTitle = first.Title;
        Title = $"LP Automation — {first.Title}";
    }

    [RelayCommand]
    private void Navigate(NavItem item)
    {
        CurrentPage = item.Create();
        CurrentPageTitle = item.Title;
        Title = $"LP Automation — {item.Title}";
    }

    [RelayCommand]
    private async Task TestApiAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        TestApiButtonText = "Testing...";
        StatusKind = "Running";
        StatusMessage = "Calling /api/recommendations...";

        try
        {
            var started = DateTimeOffset.UtcNow;
            var raw = await _recsApi.GetLatestRawAsync(20);
            var elapsed = (DateTimeOffset.UtcNow - started).TotalMilliseconds;

            ApiCallCount++;
            LastPayloadChars = raw.Length;
            LastElapsedMs = elapsed;
            LastUpdatedUtc = DateTimeOffset.UtcNow.ToString("u");
            PreviewTitle = "API Preview (truncated to 700 chars)";
            LastApiPreview = raw.Length <= 700 ? raw : raw[..700] + " ...[truncated]";

            StatusKind = "Success";
            StatusMessage = $"OK: {LastPayloadChars} chars in {LastElapsedMs:F0} ms";
        }
        catch (Exception ex)
        {
            ApiCallCount++;
            ApiErrorCount++;
            LastUpdatedUtc = DateTimeOffset.UtcNow.ToString("u");

            StatusKind = "Error";
            StatusMessage = $"API error: {ex.Message}";
            PreviewTitle = "API Preview (error)";
            LastApiPreview = "(No response body captured due to error.)";
            LastPayloadChars = 0;
            LastElapsedMs = 0;
        }
        finally
        {
            IsBusy = false;
            TestApiButtonText = "Check Connection";
        }
    }

    public sealed record NavItem(string Title, string IconKey, Func<object?> Create);
}