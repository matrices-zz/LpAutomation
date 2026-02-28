using System;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LpAutomation.Desktop.Avalonia.Services;
using LpAutomation.Desktop.Avalonia.ViewModels;
using LpAutomation.Desktop.Avalonia.Views;

namespace LpAutomation.Desktop.Avalonia;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new SimpleServiceProvider();

        var http = new HttpClient
        {
            BaseAddress = new Uri("https://localhost:7069/")
        };

        // Core services
        services.AddSingleton(http);
        services.AddSingleton(new ConfigApiClient(http));
        services.AddSingleton(new RecommendationsApiClient(http));
        services.AddSingleton(new PaperPositionsApiClient(http));
        services.AddSingleton<IFileDialogService>(new AvaloniaFileDialogService());

        // Page VMs
        services.AddTransient<RecommendationsPageViewModel>(() =>
            new RecommendationsPageViewModel(services.Get<RecommendationsApiClient>()));

        services.AddTransient<PaperPositionsPageViewModel>(() =>
            new PaperPositionsPageViewModel(services.Get<PaperPositionsApiClient>()));

        services.AddTransient<TrackPageViewModel>(() =>
            new TrackPageViewModel(
                services.Get<ConfigApiClient>(),
                services.Get<RecommendationsApiClient>(),
                services.Get<PaperPositionsApiClient>()));

        services.AddTransient<SettingsPageViewModel>(() =>
    new SettingsPageViewModel(services.Get<ConfigApiClient>()));

        // Shell VM (Phase 2.1 constructor signature)
        services.AddTransient<ShellViewModel>(() =>
            new ShellViewModel(
                services.Get<ConfigApiClient>(),
                services.Get<RecommendationsApiClient>(),
                services.Get<IFileDialogService>(),
                services.Get<TrackPageViewModel>(),
                services.Get<RecommendationsPageViewModel>(),
                services.Get<PaperPositionsPageViewModel>(),
                services.Get<SettingsPageViewModel>()));
                

        Services = services;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = services.Get<ShellViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}