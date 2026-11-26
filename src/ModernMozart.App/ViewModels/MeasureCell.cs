using ModernMozart.Core;

namespace ModernMozart.App.ViewModels;

/// <summary>One of the sixteen positions in the grid.</summary>
public sealed class MeasureCell(RollEntry entry) : ObservableObject
{
    private RollEntry _entry = entry;
    private bool _isCurrent;

    public RollEntry Entry
    {
        get => _entry;
        set { if (Set(ref _entry, value)) Raise(nameof(Measure), nameof(Die1), nameof(Die2)); }
    }

    public int    Position => _entry.Position;
    public int    Measure  => _entry.Measure;
    public int    Die1     => _entry.Die1;
    public int    Die2     => _entry.Die2;

    public bool IsCurrent
    {
        get => _isCurrent;
        set => Set(ref _isCurrent, value);
    }
}
