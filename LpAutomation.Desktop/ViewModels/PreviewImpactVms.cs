using System.Collections.Generic;
using System.Collections.ObjectModel;
using LpAutomation.Core.Diff;
using LpAutomation.Core.Models;

namespace LpAutomation.Desktop.ViewModels;

public sealed class DiffItemVm
{
    public string Path { get; }
    public string OldValue { get; }
    public string NewValue { get; }
    public DiffItemVm(string path, string oldValue, string newValue)
        => (Path, OldValue, NewValue) = (path, oldValue, newValue);
}

public sealed class PoolImpactPreviewVm
{
    public string PoolLabel { get; }
    public ObservableCollection<DiffItemVm> Changes { get; } = new();

    private PoolImpactPreviewVm(string label, IEnumerable<DiffItem> diffs)
    {
        PoolLabel = label;
        foreach (var d in diffs)
            Changes.Add(new DiffItemVm(d.Path, d.OldValue, d.NewValue));
    }

    public static PoolImpactPreviewVm FromPool(PoolKey pool, IEnumerable<DiffItem> diffs)
        => new($"{pool.Token0}/{pool.Token1} ({pool.FeeTier}) chain:{pool.ChainId}", diffs);

    public static PoolImpactPreviewVm FromGlobal(IEnumerable<DiffItem> diffs)
        => new("GLOBAL", diffs);
}
