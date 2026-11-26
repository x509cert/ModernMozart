using MeltySynth;
using ModernMozart.Core;

namespace ModernMozart.Core.Tests;

public class DiceTableTests
{
    [Fact]
    public void Table_is_a_permutation_of_1_to_176()
    {
        var all = (from sum in Enumerable.Range(2, 11)
                   from pos in Enumerable.Range(1, 16)
                   select DiceTable.Lookup(sum, pos)).ToList();

        Assert.Equal(Enumerable.Range(1, 176), all.Order());
    }

    [Fact]
    public void Seeded_roll_is_deterministic()
        => Assert.Equal(DiceTable.Roll(new Random(42)).Entries.AsEnumerable(), DiceTable.Roll(new Random(42)).Entries.AsEnumerable());

    [Fact]
    public void Reroll_changes_only_the_requested_position()
    {
        var roll = DiceTable.Roll(new Random(1));
        var re   = roll.Reroll(5, new Random(99));

        for (int i = 0; i < WaltzRoll.Length; i++)
            if (i != 4) Assert.Equal(roll.Entries[i], re.Entries[i]);
        Assert.Equal(5, re.Entries[4].Position);
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(13, 1)] [InlineData(7, 0)] [InlineData(7, 17)]
    public void Lookup_rejects_out_of_range(int sum, int pos)
        => Assert.Throws<ArgumentOutOfRangeException>(() => DiceTable.Lookup(sum, pos));
}

public class PerformancePlanTests
{
    private static readonly WaltzRoll Roll = DiceTable.Roll(new Random(42));

    [Theory]
    [InlineData(1, 16)]
    [InlineData(2, 32)]
    public void Slot_count_follows_passes(int passes, int expected)
        => Assert.Equal(expected, PerformancePlan.Create(Roll, 138, passes).Slots.Length);

    [Fact]
    public void Repeats_are_AABB()
    {
        var order = PerformancePlan.Create(Roll, 138, 2).Slots
            .Chunk(8).Select(c => $"{c[0].Entry.Section}{c[0].Pass}");
        Assert.Equal(new[] { "A1", "A2", "B1", "B2" }, order);
    }

    [Fact]
    public void Events_are_sorted_with_note_offs_first()
    {
        var ev = PerformancePlan.Create(Roll).Events;
        for (int i = 1; i < ev.Length; i++)
        {
            Assert.True(ev[i].Tick >= ev[i - 1].Tick);
            if (ev[i].Tick == ev[i - 1].Tick)
                Assert.False(!ev[i - 1].IsNoteOff && ev[i].IsNoteOff, $"note-on before note-off at tick {ev[i].Tick}");
        }
    }

    [Fact]
    public void Every_note_on_is_released()
    {
        var ev = PerformancePlan.Create(Roll).Events;
        Assert.Equal(ev.Count(e => e.Command == 0x90), ev.Count(e => e.IsNoteOff));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Closing_chord_is_held_past_the_final_barline(int passes)
    {
        var plan = PerformancePlan.Create(Roll, 138, passes);
        var heldTo = plan.Events.Where(e => e.IsNoteOff && e.Tick == plan.EndTick).ToList();

        Assert.True(plan.EndTick > plan.TotalTicks);
        Assert.InRange((plan.Length - TimeSpan.FromSeconds(plan.TotalTicks * plan.SecondsPerTick)).TotalSeconds,
                       PerformancePlan.FinalHold.TotalSeconds - 0.01, PerformancePlan.FinalHold.TotalSeconds + 0.01);
        Assert.Contains(heldTo, e => e.Channel == PerformancePlan.RightHand);
        Assert.Contains(heldTo, e => e.Channel == PerformancePlan.LeftHand);

        // The held left hand includes the bar's bass note, the lowest pitch held.
        var bar = MeasureLibrary.Get(plan.Slots[^1].Entry.Measure);
        int bass = bar.Where(n => n.Channel == PerformancePlan.LeftHand).MinBy(n => n.AbsTick * 1000 + n.Pitch).Pitch;
        Assert.Contains(heldTo, e => e.Channel == PerformancePlan.LeftHand && e.Data1 == bass);

        // Nothing else is held: every other note ends within the written music.
        Assert.All(plan.Events.Where(e => e.IsNoteOff && e.Tick != plan.EndTick), e => Assert.True(e.Tick <= plan.TotalTicks));
    }

    [Fact]
    public void Slot_lookup_maps_time_to_bar()
    {
        var plan = PerformancePlan.Create(Roll, 120, 2);   // 1.5 s per bar
        Assert.Equal(0,  plan.SlotAt(TimeSpan.Zero));
        Assert.Equal(3,  plan.SlotAt(TimeSpan.FromSeconds(5.0)));
        Assert.Equal(31, plan.SlotAt(TimeSpan.FromHours(1)));
    }
}

public class MidiFileWriterTests
{
    [Fact]
    public void Output_parses_and_matches_plan_length()
    {
        var plan = PerformancePlan.Create(DiceTable.Roll(new Random(7)), 138, 2);
        var midi = new MidiFile(new MemoryStream(MidiFileWriter.Build(plan)));
        Assert.InRange(midi.Length.TotalSeconds, plan.Length.TotalSeconds - 0.05, plan.Length.TotalSeconds + 0.05);
    }
}

public class ScoreEngraverTests
{
    private sealed class CountingCanvas : ModernMozart.Core.Notation.IScoreCanvas
    {
        public int Heads, Lines, Beams, Texts;
        public void Line(float x1, float y1, float x2, float y2, float t) => Lines++;
        public void Glyph(ModernMozart.Core.Notation.Glyph g, float x, float y, float s)
        {
            Assert.True(float.IsFinite(x) && float.IsFinite(y));
            if (g is ModernMozart.Core.Notation.Glyph.NoteheadBlack or ModernMozart.Core.Notation.Glyph.NoteheadHalf) Heads++;
        }
        public void Beam(float x1, float y1, float x2, float y2, float t) => Beams++;
        public void Text(string s, float x, float y, float size) => Texts++;
    }

    [Fact]
    public void Every_measure_engraves_one_head_per_note()
    {
        var all = Enumerable.Range(1, 176).ToArray();
        var canvas = new CountingCanvas();
        var engraver = new ModernMozart.Core.Notation.ScoreEngraver(6f);
        engraver.Draw(canvas, all, 360f);

        int expected = all.Sum(m => MeasureLibrary.Get(m).Select(n => (n.Channel, n.AbsTick, n.Pitch)).Distinct().Count());
        Assert.Equal(expected, canvas.Heads);
        Assert.Equal(176, canvas.Texts);
        Assert.True(canvas.Beams > 0);
    }

    [Theory]
    [InlineData(330f, 2)]   // phone portrait
    [InlineData(800f, 3)]   // tablet
    public void Bars_per_row_scales_with_width(float width, int expected)
    {
        var e = new ModernMozart.Core.Notation.ScoreEngraver(ModernMozart.Core.Notation.ScoreEngraver.StaffSpaceFor(width));
        Assert.Equal(expected, e.BarsPerRow(width));
        Assert.True(e.Height(16, width) > 0);
    }

    private sealed class HeadCapture : ModernMozart.Core.Notation.IScoreCanvas
    {
        public readonly List<(float X, float Y)> Heads = [];
        public void Line(float x1, float y1, float x2, float y2, float t) { }
        public void Glyph(ModernMozart.Core.Notation.Glyph g, float x, float y, float s)
        { if (g == ModernMozart.Core.Notation.Glyph.NoteheadBlack) Heads.Add((x, y)); }
        public void Beam(float x1, float y1, float x2, float y2, float t) { }
        public void Text(string s, float x, float y, float size) { }
    }

    [Fact]
    public void Playhead_at_bar_start_passes_through_first_notehead_centre()
    {
        var all = DiceTable.Roll(new Random(3)).Measures.ToArray();
        const float width = 330f;

        // Whole score, auto bars per line…
        AssertAligned(new ModernMozart.Core.Notation.ScoreEngraver(ModernMozart.Core.Notation.ScoreEngraver.StaffSpaceFor(width)), all, width);

        // …and the app's four-bar lines.
        for (int g = 0; g < 4; g++)
            AssertAligned(new ModernMozart.Core.Notation.ScoreEngraver(ModernMozart.Core.Notation.ScoreEngraver.StaffSpaceToFit(width, 4))
                          { FixedBarsPerRow = 4, FirstBarIndex = g * 4, PieceLength = 16 },
                          all.Skip(g * 4).Take(4).ToArray(), width);
    }

    private static void AssertAligned(ModernMozart.Core.Notation.ScoreEngraver e, int[] measures, float width)
    {
        var cap = new HeadCapture();
        e.Draw(cap, measures, width);

        for (int i = 0; i < measures.Length; i++)
        {
            var bar = e.Bar(i, width);
            float firstHead = cap.Heads
                .Where(h => h.X >= bar.X && h.X < bar.X + bar.Width && h.Y >= bar.SystemTop
                            && h.Y < bar.SystemTop + ModernMozart.Core.Notation.ScoreEngraver.SystemHeight * e.S)
                .Min(h => h.X);
            Assert.InRange(bar.At(0) - (firstHead + 0.59f * e.S), -0.01f, 0.01f);
        }
    }
}

public class PlaybackClockTests
{
    [Fact]
    public void Silent_until_the_device_reports_audio()
    {
        double now = 0;
        var clock = new PlaybackClock(() => now);
        clock.Report(TimeSpan.Zero);
        now = 0.5;
        Assert.Equal(TimeSpan.Zero, clock.Now());
    }

    [Fact]
    public void Smooths_bursty_reports_into_even_frames()
    {
        // Device reports what's been heard in 40 ms bursts, a few ms late and uneven;
        // the UI samples every 16.7 ms.
        double now = 0;
        var clock = new PlaybackClock(() => now);
        var rng = new Random(1);
        double nextReport = 0.04;
        var frames = new List<double>();

        for (now = 0; now < 5; now += 1 / 60.0)
        {
            while (nextReport <= now)
            {
                double heard = Math.Floor(nextReport / 0.04) * 0.04;      // quantised position
                double at = now;
                clock.Report(TimeSpan.FromSeconds(heard));
                nextReport += 0.04 + (rng.NextDouble() - 0.5) * 0.02;     // ±10 ms jitter
                _ = at;
            }
            frames.Add(clock.Now().TotalSeconds);
        }

        var steps = frames.Skip(30).Zip(frames.Skip(31), (a, b) => b - a).ToArray();
        Assert.All(steps, d => Assert.True(d >= 0, "never moves backwards"));
        double mean = steps.Average(), jitter = Math.Sqrt(steps.Average(d => (d - mean) * (d - mean)));
        Assert.InRange(mean, 1 / 60.0 * 0.95, 1 / 60.0 * 1.05);
        Assert.True(jitter < 0.004, $"frame-to-frame jitter {jitter * 1000:F1} ms");   // raw reports: ~19 ms
        Assert.InRange(frames[^1] - now, -0.06, 0.06);                                  // stays on time
    }

    [Fact]
    public void Snaps_on_a_real_jump()
    {
        double now = 0;
        var clock = new PlaybackClock(() => now);
        clock.Report(TimeSpan.FromSeconds(0.1));
        now = 0.2;
        clock.Report(TimeSpan.FromSeconds(2.0));     // e.g. device skipped ahead
        Assert.InRange(clock.Now().TotalSeconds, 1.99, 2.01);
    }
}

