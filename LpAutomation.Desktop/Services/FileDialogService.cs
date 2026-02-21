using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace LpAutomation.Desktop.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? ShowOpenJson(string title = "Open config")
    {
        var dlg = new OpenFileDialog
        {
            Title = title,
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? ShowSaveJson(string defaultFileName = "lpautomation-config.json", string title = "Save config")
    {
        var dlg = new SaveFileDialog
        {
            Title = title,
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = defaultFileName,
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = true
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);

    public Task WriteAllTextAsync(string path, string contents) => File.WriteAllTextAsync(path, contents);
}
