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

        services.AddSingleton(http);
        services.AddSingleton(new ConfigApiClient(http));
        services.AddSingleton(new RecommendationsApiClient(http));
        services.AddSingleton<IFileDialogService>(new AvaloniaFileDialogService());
             
        services.AddTransient<RecommendationsPageViewModel>(() =>
    new RecommendationsPageViewModel(services.Get<RecommendationsApiClient>()));

        services.AddTransient<ShellViewModel>(() =>
            new ShellViewModel(
                services.Get<ConfigApiClient>(),
                services.Get<RecommendationsApiClient>(),
                services.Get<IFileDialogService>(),
                services.Get<RecommendationsPageViewModel>()));

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
