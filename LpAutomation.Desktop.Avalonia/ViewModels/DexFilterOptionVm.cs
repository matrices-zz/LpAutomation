using CommunityToolkit.Mvvm.ComponentModel;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class DexFilterOptionVm : ObservableObject
{
    public DexFilterOptionVm(string name, bool isSelected = true)
    {
        Name = name;
        IsSelected = isSelected;
    }

    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;
}
