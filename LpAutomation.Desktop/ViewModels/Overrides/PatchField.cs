using LpAutomation.Desktop.MVVM;

namespace LpAutomation.Desktop.ViewModels.Overrides;

public sealed class PatchField<T> : ObservableObject
{
    private bool _isOverridden;
    private T _value;

    public bool IsOverridden { get => _isOverridden; set => Set(ref _isOverridden, value); }
    public T Value { get => _value; set => Set(ref _value, value); }

    public PatchField(T defaultValue)
    {
        _value = defaultValue;
        _isOverridden = false;
    }
}
