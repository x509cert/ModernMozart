using System.Collections.ObjectModel;
using ModernMozart.Core;

namespace ModernMozart.App.ViewModels;

/// <summary>Four positions of the roll: their dice cells and the matching line of score.</summary>
public sealed class BarGroup(int index) : ObservableObject
{
    public const int Size = 4;

    private IReadOnlyList<int> _measures = [];
    private double _playhead = -1;

    public int    FirstBarIndex => index * Size;
    public int    PieceLength   => WaltzRoll.Length;
    public int    BarsPerRow    => Size;
    public string Caption => $"PART {(FirstBarIndex < WaltzRoll.HalfLength ? 'A' : 'B')}  ·  BARS {FirstBarIndex + 1}–{FirstBarIndex + Size}";

    public ObservableCollection<MeasureCell> Cells { get; } = [];

    /// <summary>The four measure numbers (a new list on every change, for the score view).</summary>
    public IReadOnlyList<int> Measures { get => _measures; private set => Set(ref _measures, value); }

    /// <summary>Bar (0–3) + fraction being played in this line, or -1.</summary>
    public double Playhead { get => _playhead; set => Set(ref _playhead, value); }

    public void Load(IEnumerable<RollEntry> entries)
    {
        Cells.Clear();
        foreach (var e in entries) Cells.Add(new MeasureCell(e));
        Refresh();
    }

    public void Refresh() => Measures = [.. Cells.Select(c => c.Measure)];
}
