using System.IO;
using System.Threading.Tasks;

namespace LpAutomation.Desktop.Avalonia.Services;

public sealed class AvaloniaFileDialogService : IFileDialogService
{
    // TEMP for Phase 1.6: return null until we wire Avalonia StorageProvider dialogs
    public Task<string?> ShowOpenJsonAsync(string title = "Open config")
        => Task.FromResult<string?>(null);

    public Task<string?> ShowSaveJsonAsync(string defaultFileName = "lpautomation-config.json", string title = "Save config")
        => Task.FromResult<string?>(null);

    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);

    public Task WriteAllTextAsync(string path, string contents) => File.WriteAllTextAsync(path, contents);
}
