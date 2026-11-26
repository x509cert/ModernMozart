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

    /// <summary>How long the closing chord is held past the final barline, as under a fermata.</summary>
    public static readonly TimeSpan FinalHold = TimeSpan.FromSeconds(2.5);

    /// <summary>Ticks of written music (up to the final barline).</summary>
    public long TotalTicks      => (long)Slots.Length * TicksPerMeasure;
    /// <summary>Ticks until the held closing chord is released.</summary>
    public long EndTick         => TotalTicks + HoldTicks(Bpm);
    public double SecondsPerTick => SecondsPerTickAt(Bpm);
    public TimeSpan MeasureLength => TimeSpan.FromSeconds(TicksPerMeasure * SecondsPerTick);
    public TimeSpan Length        => TimeSpan.FromSeconds(EndTick * SecondsPerTick);

    private static double SecondsPerTickAt(int bpm) => 60.0 / (bpm * TicksPerQuarter);
    private static long HoldTicks(int bpm) => (long)Math.Round(FinalHold.TotalSeconds / SecondsPerTickAt(bpm));

    private PerformancePlan(WaltzRoll roll, int bpm, int passes)
    {
        Roll   = roll;
        Bpm    = bpm;
        Passes = passes;
        Slots  = ExpandRepeats(roll, passes);
        Events = Schedule(Slots, EndTick);
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

    /// <summary>
    /// Notes that ring on at the very end: each hand's last chord, plus the left hand's bass
    /// note from the start of the bar, so the held sound is a full chord rather than an
    /// unsupported treble.
    /// </summary>
    private static HashSet<NoteEvent> ClosingChord(NoteEvent[] bar)
    {
        var held = new HashSet<NoteEvent>();
        foreach (byte hand in (byte[])[RightHand, LeftHand])
        {
            var notes = bar.Where(n => n.Channel == hand).ToArray();
            if (notes.Length == 0) continue;
            long last = notes.Max(n => n.AbsTick);
            held.UnionWith(notes.Where(n => n.AbsTick == last));
        }

        var leftHand = bar.Where(n => n.Channel == LeftHand).ToArray();
        if (leftHand.Length > 0)
        {
            long first = leftHand.Min(n => n.AbsTick);
            held.Add(leftHand.Where(n => n.AbsTick == first).MinBy(n => n.Pitch));
        }
        return held;
    }

    private static ImmutableArray<MidiEvent> Schedule(ImmutableArray<PerformanceSlot> slots, long endTick)
    {
        var events = new List<MidiEvent>(slots.Length * 24);

        foreach (var slot in slots)
        {
            long barStart = (long)slot.Index * TicksPerMeasure;
            var notes = MeasureLibrary.Get(slot.Entry.Measure);
            var held  = slot.Index == slots.Length - 1 ? ClosingChord(notes) : [];

            foreach (var n in notes)
            {
                long on  = barStart + n.AbsTick;
                long off = held.Contains(n) ? endTick : on + Math.Max(1, n.DurTicks);
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
