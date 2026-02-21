using System;
using LpAutomation.Core.Models;
using LpAutomation.Desktop.MVVM;

namespace LpAutomation.Desktop.ViewModels.Overrides;

public sealed class PoolOverrideVm : ObservableObject
{
    public Guid OverrideId { get; }
    public PoolKey PoolKey { get; }

    private bool _enabled;
    private string _notes;

    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public string Notes { get => _notes; set => Set(ref _notes, value); }

    private GlobalStrategyConfigPatch _patch;
    public GlobalStrategyConfigPatch Patch { get => _patch; private set => Set(ref _patch, value); }

    public string PoolLabel => $"{PoolKey.Token0}/{PoolKey.Token1} ({PoolKey.FeeTier}) chain:{PoolKey.ChainId}";

    public PoolOverrideVm(PoolOverride ov)
    {
        OverrideId = ov.OverrideId;
        PoolKey = ov.PoolKey;
        _enabled = ov.Enabled;
        _notes = ov.Notes;
        _patch = ov.Patch;
    }

    public void SetPatch(GlobalStrategyConfigPatch patch) => Patch = patch;

    public PoolOverride ToModel() => new()
    {
        OverrideId = OverrideId,
        PoolKey = PoolKey,
        Enabled = Enabled,
        Notes = Notes,
        Patch = Patch
    };
}
