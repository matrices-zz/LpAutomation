using LpAutomation.Core.Models;
using LpAutomation.Desktop.MVVM;

namespace LpAutomation.Desktop.ViewModels.Overrides;

public sealed class OverrideEditorViewModel : ObservableObject
{
    private PoolOverrideVm? _selected;
    public PoolOverrideVm? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
                LoadFromSelected();
        }
    }

    // Decisions
    public PatchField<int> CompoundScoreMin { get; } = new(70);
    public PatchField<int> ReallocateScoreMax { get; } = new(40);
    public PatchField<int> MinHoursBetweenCompounds { get; } = new(6);

    // Guardrails
    public PatchField<int> MaxSlippageBps { get; } = new(50);
    public PatchField<decimal> MaxNotionalUsdPerProposal { get; } = new(2500m);

    public RelayCommand ApplyPatchCommand { get; }

    public OverrideEditorViewModel()
    {
        ApplyPatchCommand = new RelayCommand(ApplyPatch, () => Selected != null);
    }

    private void LoadFromSelected()
    {
        ApplyPatchCommand.RaiseCanExecuteChanged();
        ResetFields();
        if (Selected is null) return;

        var p = Selected.Patch;

        if (p.Decisions?.CompoundScoreMin is int c) { CompoundScoreMin.IsOverridden = true; CompoundScoreMin.Value = c; }
        if (p.Decisions?.ReallocateScoreMax is int r) { ReallocateScoreMax.IsOverridden = true; ReallocateScoreMax.Value = r; }
        if (p.Decisions?.MinHoursBetweenCompounds is int h) { MinHoursBetweenCompounds.IsOverridden = true; MinHoursBetweenCompounds.Value = h; }

        if (p.Guardrails?.MaxSlippageBps is int s) { MaxSlippageBps.IsOverridden = true; MaxSlippageBps.Value = s; }
        if (p.Guardrails?.MaxNotionalUsdPerProposal is decimal n) { MaxNotionalUsdPerProposal.IsOverridden = true; MaxNotionalUsdPerProposal.Value = n; }
    }

    private void ResetFields()
    {
        CompoundScoreMin.IsOverridden = false;
        ReallocateScoreMax.IsOverridden = false;
        MinHoursBetweenCompounds.IsOverridden = false;
        MaxSlippageBps.IsOverridden = false;
        MaxNotionalUsdPerProposal.IsOverridden = false;
    }

    private void ApplyPatch()
    {
        if (Selected is null) return;

        DecisionConfigPatch? decisions = null;
        if (CompoundScoreMin.IsOverridden || ReallocateScoreMax.IsOverridden || MinHoursBetweenCompounds.IsOverridden)
        {
            decisions = new DecisionConfigPatch
            {
                CompoundScoreMin = CompoundScoreMin.IsOverridden ? CompoundScoreMin.Value : null,
                ReallocateScoreMax = ReallocateScoreMax.IsOverridden ? ReallocateScoreMax.Value : null,
                MinHoursBetweenCompounds = MinHoursBetweenCompounds.IsOverridden ? MinHoursBetweenCompounds.Value : null
            };
        }

        GuardrailsConfigPatch? guardrails = null;
        if (MaxSlippageBps.IsOverridden || MaxNotionalUsdPerProposal.IsOverridden)
        {
            guardrails = new GuardrailsConfigPatch
            {
                MaxSlippageBps = MaxSlippageBps.IsOverridden ? MaxSlippageBps.Value : null,
                MaxNotionalUsdPerProposal = MaxNotionalUsdPerProposal.IsOverridden ? MaxNotionalUsdPerProposal.Value : null
            };
        }

        var patch = new GlobalStrategyConfigPatch
        {
            Decisions = decisions,
            Guardrails = guardrails
        };

        Selected.SetPatch(patch);
    }
}
