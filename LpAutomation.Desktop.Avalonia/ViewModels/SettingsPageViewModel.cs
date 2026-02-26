using CommunityToolkit.Mvvm.ComponentModel;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Settings";
}
