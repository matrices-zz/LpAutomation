namespace LpAutomation.Desktop.ViewModels;

public sealed class ValidationIssueVm
{
    public string Severity { get; }
    public string Path { get; }
    public string Message { get; }

    public ValidationIssueVm(string severity, string path, string message)
        => (Severity, Path, Message) = (severity, path, message);
}
