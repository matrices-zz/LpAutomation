using CommunityToolkit.Mvvm.ComponentModel;

namespace LpAutomation.Desktop.ViewModels;

public partial class DashboardPageViewModel : ObservableObject
{
    // These are currently placeholders so bindings resolve.
    // Later we can populate from RecommendationsApiClient and/or a dashboard endpoint.

    [ObservableProperty]
    private string _currentRegime = "—";

    [ObservableProperty]
    private string _topReinvestScore = "—";

    [ObservableProperty]
    private string _topReallocateScore = "—";
}