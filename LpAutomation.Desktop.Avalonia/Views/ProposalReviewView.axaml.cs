using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LpAutomation.Desktop.Avalonia.Views;

public partial class ProposalReviewView : Window
{
    public ProposalReviewView()
    {
        InitializeComponent();

        // Wire up the cancel button manually if needed, or just use the XAML name
        this.FindControl<Button>("CancelButton").Click += (s, e) => Close();
    }
}