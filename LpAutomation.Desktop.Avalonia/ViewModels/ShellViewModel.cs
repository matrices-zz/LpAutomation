using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Desktop.Avalonia.Services;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly ConfigApiClient _configApi;
    private readonly RecommendationsApiClient _recsApi;
    private readonly IFileDialogService _files;

    [ObservableProperty]
    private string _title = "LP Automation — Avalonia";

    [ObservableProperty]
    private string _subtitle = "Desktop MVP (Phase 1!)";

    [ObservableProperty]
    private string _currentPageTitle = "Dashboard";

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public ShellViewModel(
    ConfigApiClient configApi,
    RecommendationsApiClient recsApi,
    IFileDialogService files)
    {
        _configApi = configApi;
        _recsApi = recsApi;
        _files = files;

        NavItems.Add(new NavItem("Dashboard", "dashboard"));
        NavItems.Add(new NavItem("Recommendations", "star"));
        NavItems.Add(new NavItem("Config", "file"));
        NavItems.Add(new NavItem("Settings", "settings"));
    }


    [RelayCommand]
    private void Navigate(NavItem item)
    {
        CurrentPageTitle = item.Title;
        Title = $"LP Automation — {item.Title}";
    }

    public sealed record NavItem(string Title, string IconKey);
}
