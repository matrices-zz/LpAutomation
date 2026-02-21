using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Desktop.Services;
using MaterialDesignThemes.Wpf;

namespace LpAutomation.Desktop.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly ConfigApiClient _api;
    private readonly IFileDialogService _files;
    private readonly RecommendationsApiClient _recsApi;

    [ObservableProperty]
    private bool _isNavOpen = true;

    [ObservableProperty]
    private string _title = "LP Automation";

    [ObservableProperty]
    private string _subtitle = "Desktop MVP";

    [ObservableProperty]
    private object? _currentPage;

    // This is what ShellWindow.xaml binds to in the top app bar
    [ObservableProperty]
    private string _currentPageTitle = "Dashboard";

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public ShellViewModel(ConfigApiClient api, RecommendationsApiClient recsApi, IFileDialogService fileDialogs)
    {
        _api = api;
        _recsApi = recsApi;
        _files = fileDialogs;

        NavItems.Add(new NavItem("Dashboard", PackIconKind.ViewDashboard, () => new DashboardPageViewModel()));
        NavItems.Add(new NavItem("Recommendations", PackIconKind.StarOutline, () => new RecommendationsPageViewModel(_recsApi)));
        NavItems.Add(new NavItem("Config", PackIconKind.FileDocumentOutline, () => new ConfigPageViewModel()));
        NavItems.Add(new NavItem("Settings", PackIconKind.CogOutline, () => new SettingsViewModel(_api, _files)));

        // Default page
        var first = NavItems[0];
        CurrentPage = first.Create();
        CurrentPageTitle = first.Title;
        Title = $"LP Automation — {first.Title}";
    }

    [RelayCommand]
    private void Navigate(NavItem item)
    {
        CurrentPage = item.Create();
        CurrentPageTitle = item.Title;

        IsNavOpen = false;
        Title = $"LP Automation — {item.Title}";
    }

    // ShellWindow.xaml binds to IconKind and Title
    public sealed record NavItem(string Title, PackIconKind IconKind, Func<object> Create);
}