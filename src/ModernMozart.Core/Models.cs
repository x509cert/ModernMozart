using System.Collections.Immutable;

namespace ModernMozart.Core;

/// <summary>One note in a measure, timed relative to the start of that 3/4 bar.</summary>
public readonly record struct NoteEvent(
    long AbsTick,
    byte Pitch,
    int  DurTicks,
    byte Velocity,
    byte Channel);

/// <summary>The outcome of one 2d6 throw for one of the sixteen positions.</summary>
public readonly record struct RollEntry(int Position, int Die1, int Die2, int Measure)
{
    public int Sum => Die1 + Die2;

    /// <summary>'A' for positions 1–8, 'B' for 9–16.</summary>
    public char Section => Position <= WaltzRoll.HalfLength ? 'A' : 'B';
}

/// <summary>A complete game: sixteen throws, sixteen measures. Immutable.</summary>
public sealed record WaltzRoll(ImmutableArray<RollEntry> Entries)
{
    public const int Length     = 16;
    public const int HalfLength = Length / 2;

    public IEnumerable<int> Measures => Entries.Select(e => e.Measure);

    /// <summary>Returns a copy with one position (1-based) re-thrown.</summary>
    public WaltzRoll Reroll(int position, Random rng)
        => this with { Entries = Entries.SetItem(position - 1, DiceTable.Throw(rng, position)) };
}

/// <summary>One bar of the actual performance (after AABB repeats are expanded).</summary>
public sealed record PerformanceSlot(
    int      Index,     // 0-based index in the performance
    RollEntry Entry,    // which throw produced this bar
    int      Pass);     // 1 or 2 (repeat number)

/// <summary>A channel message at an absolute tick in the performance.</summary>
public readonly record struct MidiEvent(long Tick, byte Status, byte Data1, byte Data2)
{
    public int  Channel   => Status & 0x0F;
    public int  Command   => Status & 0xF0;
    public bool IsNoteOff => Command == 0x80;
}
