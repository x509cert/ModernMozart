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
}
