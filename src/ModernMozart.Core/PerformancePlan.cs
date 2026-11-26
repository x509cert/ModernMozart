using System.Collections.Immutable;

namespace ModernMozart.Core;

/// <summary>
/// A fully scheduled performance of a <see cref="WaltzRoll"/>: the bar order after
/// repeats (AABB) and every MIDI channel event at an absolute tick. Both the live
/// synthesizer and the MIDI file writer consume this, so they can never disagree.
/// </summary>
public sealed class PerformancePlan
{
    public const int TicksPerQuarter = MeasureLibrary.PPQ;
    public const int TicksPerMeasure = MeasureLibrary.DH;   // one 3/4 bar

    public const byte RightHand = 0;
    public const byte LeftHand  = 1;

    public WaltzRoll Roll { get; }
    public int Bpm { get; }
    public int Passes { get; }
    public ImmutableArray<PerformanceSlot> Slots { get; }

    /// <summary>Sorted by tick; at equal ticks note-offs come first so repeated notes re-strike.</summary>
    public ImmutableArray<MidiEvent> Events { get; }

    public long TotalTicks      => (long)Slots.Length * TicksPerMeasure;
    public double SecondsPerTick => 60.0 / (Bpm * TicksPerQuarter);
    public TimeSpan MeasureLength => TimeSpan.FromSeconds(TicksPerMeasure * SecondsPerTick);
    public TimeSpan Length        => TimeSpan.FromSeconds(TotalTicks * SecondsPerTick);

    private PerformancePlan(WaltzRoll roll, int bpm, int passes)
    {
        Roll   = roll;
        Bpm    = bpm;
        Passes = passes;
        Slots  = ExpandRepeats(roll, passes);
        Events = Schedule(Slots);
    }

    /// <param name="passes">1 = straight through, 2 = observe both repeats (AABB).</param>
    public static PerformancePlan Create(WaltzRoll roll, int bpm = 138, int passes = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bpm, 20);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bpm, 400);
        ArgumentOutOfRangeException.ThrowIfLessThan(passes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(passes, 2);
        return new(roll, bpm, passes);
    }

    /// <summary>The bar sounding at a given playback time (clamped to the performance).</summary>
    public int SlotAt(TimeSpan position)
        => Math.Clamp((int)(position.Ticks / MeasureLength.Ticks), 0, Slots.Length - 1);

    // ── AABB expansion ────────────────────────────────────────────────────
    private static ImmutableArray<PerformanceSlot> ExpandRepeats(WaltzRoll roll, int passes)
    {
        var a = roll.Entries[..WaltzRoll.HalfLength];
        var b = roll.Entries[WaltzRoll.HalfLength..];

        var slots = ImmutableArray.CreateBuilder<PerformanceSlot>(WaltzRoll.Length * passes);
        foreach (var section in (ImmutableArray<RollEntry>[])[a, b])
            for (int pass = 1; pass <= passes; pass++)
                foreach (var entry in section)
                    slots.Add(new PerformanceSlot(slots.Count, entry, pass));

        return slots.MoveToImmutable();
    }

    // ── Event scheduling ─────────────────────────────────────────────────
    private static ImmutableArray<MidiEvent> Schedule(ImmutableArray<PerformanceSlot> slots)
    {
        var events = new List<MidiEvent>(slots.Length * 24);

        foreach (var slot in slots)
        {
            long barStart = (long)slot.Index * TicksPerMeasure;
            foreach (var n in MeasureLibrary.Get(slot.Entry.Measure))
            {
                long on  = barStart + n.AbsTick;
                long off = on + Math.Max(1, n.DurTicks);
                events.Add(new(on,  (byte)(0x90 | n.Channel), n.Pitch, n.Velocity));
                events.Add(new(off, (byte)(0x80 | n.Channel), n.Pitch, 0));
            }
        }

        // Stable sort: tick, then note-off before note-on.
        return [.. events
            .Select((e, i) => (e, i))
            .OrderBy(x => x.e.Tick)
            .ThenBy(x => x.e.IsNoteOff ? 0 : 1)
            .ThenBy(x => x.i)
            .Select(x => x.e)];
    }
}
