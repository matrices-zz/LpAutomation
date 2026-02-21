using System;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using LpAutomation.Desktop.Services;
using LpAutomation.Desktop.ViewModels;
using LpAutomation.Desktop.Views;
using MaterialDesignThemes.Wpf;

namespace LpAutomation.Desktop;

public partial class App : Application
{
    // Simple service locator for MVP (we can upgrade to Microsoft.Extensions.Hosting later)
    public static IServiceProvider Services { get; private set; } = default!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Force Material theme (v5+ friendly)
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();

        theme.SetBaseTheme(BaseTheme.Dark);
        theme.SetPrimaryColor(System.Windows.Media.Colors.MediumPurple);
        theme.SetSecondaryColor(System.Windows.Media.Colors.LimeGreen);

        paletteHelper.SetTheme(theme);
        var services = new SimpleServiceProvider();

        // Core singletons
        var http = new HttpClient
        {
            BaseAddress = new Uri("https://localhost:7069/") // match your Server URL
        };

        services.AddSingleton(http);
        services.AddSingleton(new ConfigApiClient(http));
        services.AddSingleton(new RecommendationsApiClient(http));
        services.AddSingleton<IFileDialogService>(new FileDialogService());

        // ViewModels
        services.AddTransient<ShellViewModel>(() =>
            new ShellViewModel(
                services.Get<ConfigApiClient>(),
                services.Get<RecommendationsApiClient>(),
                services.Get<IFileDialogService>()));

        services.AddTransient<SettingsViewModel>(() =>
            new SettingsViewModel(
                services.Get<ConfigApiClient>(),
                services.Get<IFileDialogService>()));

        Services = services;

        // Start the Shell
        var shellVm = services.Get<ShellViewModel>();
        var shell = new ShellWindow { DataContext = shellVm };
        shell.Show();
    }
}
