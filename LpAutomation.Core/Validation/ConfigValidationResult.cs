using System.Collections.Generic;
using System.Linq;

namespace LpAutomation.Core.Validation;

public sealed record ValidationIssue(string Path, string Message, string Severity);

public sealed class ConfigValidationResult
{
    public List<ValidationIssue> Issues { get; } = new();
    public bool IsValid => Issues.All(i => i.Severity != "ERROR");

    public void Error(string path, string msg) => Issues.Add(new(path, msg, "ERROR"));
    public void Warn(string path, string msg) => Issues.Add(new(path, msg, "WARN"));
    public void Info(string path, string msg) => Issues.Add(new(path, msg, "INFO"));
}
