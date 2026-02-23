using System.Threading.Tasks;

namespace LpAutomation.Desktop.Avalonia.Services;

public interface IFileDialogService
{
    Task<string?> ShowOpenJsonAsync(string title = "Open config");
    Task<string?> ShowSaveJsonAsync(string defaultFileName = "lpautomation-config.json", string title = "Save config");

    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string contents);
}
