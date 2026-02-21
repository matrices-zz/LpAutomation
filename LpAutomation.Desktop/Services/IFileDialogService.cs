namespace LpAutomation.Desktop.Services;

public interface IFileDialogService
{
    string? ShowOpenJson(string title = "Open config");
    string? ShowSaveJson(string defaultFileName = "lpautomation-config.json", string title = "Save config");

    // Simple file IO helpers used by ViewModels
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string contents);
}
