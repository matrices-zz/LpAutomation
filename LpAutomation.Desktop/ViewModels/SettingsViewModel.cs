using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Desktop.Services;

namespace LpAutomation.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigApiClient _api;
    private readonly IFileDialogService _files;

    [ObservableProperty]
    private string _status = "Ready";

    public ObservableCollection<string> RecentFiles { get; } = new();

    public SettingsViewModel(ConfigApiClient api, IFileDialogService files)
    {
        _api = api;
        _files = files;
    }

    [RelayCommand]
    private async Task ExportConfigAsync()
    {
        try
        {
            Status = "Exporting config...";

            var json = await _api.ExportJsonAsync();

            var path = _files.ShowSaveJson("strategy-config.json", "Strategy Config");
            if (path is null)
            {
                Status = "Export cancelled.";
                return;
            }

            await _files.WriteAllTextAsync(path, json);
            AddRecent(path);

            Status = $"Exported to {path}";
        }
        catch (Exception ex)
        {
            Status = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportConfigAsync()
    {
        try
        {
            Status = "Importing config...";

            var path = _files.ShowOpenJson("Strategy Config");
            if (path is null)
            {
                Status = "Import cancelled.";
                return;
            }

            var json = await _files.ReadAllTextAsync(path);
            await _api.ImportJsonAsync(json);
            AddRecent(path);

            Status = $"Imported from {path}";
        }
        catch (Exception ex)
        {
            Status = $"Import failed: {ex.Message}";
        }
    }

    private void AddRecent(string path)
    {
        if (RecentFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
            return;

        RecentFiles.Insert(0, path);

        while (RecentFiles.Count > 10)
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
    }
}
