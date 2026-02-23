using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "LP Automation — Avalonia";

    [ObservableProperty]
    private string _subtitle = "Desktop MVP (Phase 1)";

    [ObservableProperty]
    private string _currentPageTitle = "Dashboard";

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public ShellViewModel()
    {
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
