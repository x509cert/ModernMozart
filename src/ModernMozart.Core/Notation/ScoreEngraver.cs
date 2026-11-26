namespace ModernMozart.Core.Notation;

/// <summary>
/// Engraves a sequence of measures as a piano grand staff (treble = right hand,
/// bass = left hand) in G major, 3/4. Version 1: proportional spacing, beams by beat,
/// notated durations taken from the onset-to-onset interval (so the left hand's
/// staccato waltz chords read as quarters, as Mozart wrote them).
///
/// Dimensions follow the SMuFL/Bravura engraving defaults; all lengths below are in
/// staff spaces and scaled by <see cref="S"/>.
/// </summary>
public sealed class ScoreEngraver(float staffSpace)
{
    // ── SMuFL engraving defaults (staff spaces) ───────────────────────────
    private const float StaffLine = 0.13f, StemW = 0.12f, LedgerW = 0.16f, LedgerExt = 0.4f;
    private const float ThinBar = 0.16f, ThickBar = 0.5f, BarSep = 0.4f;
    private const float BeamW = 0.5f, BeamStep = 0.75f;                 // thickness, beam-to-beam
    private const float HeadW = 1.18f, HeadStemDy = 0.168f;             // noteheadBlack stem anchors
    private const float StemLen = 3.5f, MinStem = 2.75f;

    // ── System layout (staff spaces) ──────────────────────────────────────
    private const float Top = 4.5f, StaffGap = 7f, Bottom = 3.5f, MarginLeft = 1.8f, MinBar = 24f;
    private const float BarPadLeft = 1.2f, BarPadTotal = 2.6f;          // notes are spaced inside this
    public const float SystemHeight = Top + 4 + StaffGap + 4 + Bottom;   // 23

    private const int TrebleBottom = 30;   // E4 (diatonic step index)
    private const int BassBottom   = 18;   // G2

    public float S { get; } = staffSpace;

    /// <summary>Force this many bars per line (0 = as many as fit).</summary>
    public int FixedBarsPerRow { get; init; }

    /// <summary>Index of the first measure drawn within the whole piece (for segments of a score).</summary>
    public int FirstBarIndex { get; init; }

    /// <summary>Bars in the whole piece (0 = just the measures drawn). Places the A/B double bar and final bar.</summary>
    public int PieceLength { get; init; }

    /// <summary>Staff space that fits two bars per row on a phone, more on wider screens.</summary>
    public static float StaffSpaceFor(float width) => Math.Clamp(width / 62f, 4.5f, 8f);

    /// <summary>Staff space that fits exactly <paramref name="bars"/> bars across, with a full header.</summary>
    public static float StaffSpaceToFit(float width, int bars)
        => Math.Clamp(width / (MarginLeft + HeaderWidth(timeSig: true) + bars * 20f + ThickBar), 3.2f, 8f);

    public int BarsPerRow(float width) => FixedBarsPerRow > 0
        ? FixedBarsPerRow
        : Math.Max(1, (int)((width / S - MarginLeft - HeaderWidth(timeSig: true)) / MinBar));

    private bool TimeSigOnRow(int row) => row == 0 && FirstBarIndex == 0;

    public float Height(int barCount, float width)
    {
        int per = BarsPerRow(width);
        return (barCount + per - 1) / per * SystemHeight * S;
    }

    /// <summary>Where a bar sits on the page, for highlighting and scrolling.</summary>
    /// <param name="NoteX">x of the first beat's notehead centre; <see cref="At"/> moves through the bar.</param>
    public readonly record struct BarBox(float X, float Width, float SystemTop, float StaffTop, float StaffBottom,
                                         float NoteX, float NoteSpan)
    {
        /// <summary>x at <paramref name="fraction"/> (0–1) of the way through the bar, aligned with note centres.</summary>
        public float At(double fraction) => NoteX + (float)Math.Clamp(fraction, 0, 1) * NoteSpan;
    }

    public BarBox Bar(int index, float width)
    {
        int per = BarsPerRow(width), row = index / per, col = index % per;
        float left   = MarginLeft * S, right = width - ThickBar * S;
        float header = HeaderWidth(TimeSigOnRow(row)) * S;
        float barW   = (right - left - header) / per;
        float bx     = left + header + barW * col;
        float top    = (row * SystemHeight + Top) * S;
        return new(bx, barW, row * SystemHeight * S, top, top + (8 + StaffGap) * S,
                   bx + (BarPadLeft + HeadW / 2) * S, barW - BarPadTotal * S);
    }

    // ── Drawing ───────────────────────────────────────────────────────────
    public void Draw(IScoreCanvas c, IReadOnlyList<int> measures, float width)
    {
        int per = BarsPerRow(width);
        float left = MarginLeft * S, right = width - ThickBar * S;

        for (int first = 0, row = 0; first < measures.Count; first += per, row++)
        {
            int count     = Math.Min(per, measures.Count - first);
            bool timeSig  = TimeSigOnRow(row);
            float top     = (row * SystemHeight + Top) * S;           // treble top line
            float bassTop = top + (4 + StaffGap) * S;
            float bottom  = bassTop + 4 * S;
            float header  = HeaderWidth(timeSig) * S;
            float barW    = (right - left - header) / per;
            float rowEnd  = left + header + barW * count;

            for (int i = 0; i < 5; i++)
            {
                c.Line(left, top + i * S, rowEnd, top + i * S, StaffLine * S);
                c.Line(left, bassTop + i * S, rowEnd, bassTop + i * S, StaffLine * S);
            }

            // Brace + system barline
            float braceScale = (bottom - top) / 4f;                      // brace glyph is 4 spaces tall
            c.Glyph(Glyph.Brace, left - 0.28f * braceScale - 0.3f * S, bottom, braceScale);
            c.Line(left, top, left, bottom, ThinBar * S);

            DrawHeader(c, left, top, bassTop, timeSig);

            for (int i = 0; i < count; i++)
            {
                int index = first + i;
                float bx  = left + header + barW * i;
                int pieceIndex = FirstBarIndex + index, total = PieceLength > 0 ? PieceLength : measures.Count;
                bool last = pieceIndex == total - 1;

                c.Text(measures[index].ToString(), bx + 0.3f * S, top - 3.0f * S, 1.4f * S);
                DrawBar(c, MeasureLibrary.Get(measures[index]), bx, barW, top, bassTop, fermata: last);

                float x = bx + barW;
                bool endOfA = pieceIndex == WaltzRoll.HalfLength - 1 && total == WaltzRoll.Length;
                if (last)
                {
                    c.Line(x - ThickBar * S / 2, top, x - ThickBar * S / 2, bottom, ThickBar * S);
                    c.Line(x - (ThickBar + BarSep) * S, top, x - (ThickBar + BarSep) * S, bottom, ThinBar * S);
                }
                else if (endOfA)
                {
                    c.Line(x, top, x, bottom, ThinBar * S);
                    c.Line(x - BarSep * S, top, x - BarSep * S, bottom, ThinBar * S);
                }
                else c.Line(x, top, x, bottom, ThinBar * S);
            }
        }
    }

    private static float HeaderWidth(bool timeSig) => 0.6f + 2.75f + 0.8f + 1.0f + 1.0f + (timeSig ? 1.8f + 1.0f : 0f);

    private void DrawHeader(IScoreCanvas c, float left, float top, float bassTop, bool timeSig)
    {
        float x = left + 0.6f * S;
        c.Glyph(Glyph.GClef, x, top + 3 * S, S);            // curls round the G line
        c.Glyph(Glyph.FClef, x, bassTop + 1 * S, S);        // dots either side of the F line
        x += (2.75f + 0.8f) * S;

        c.Glyph(Glyph.Sharp, x, top, S);                    // F♯: top line (F5)
        c.Glyph(Glyph.Sharp, x, bassTop + 1 * S, S);        // F♯: fourth line (F3)
        x += (1.0f + 1.0f) * S;

        if (!timeSig) return;
        foreach (float t in (float[])[top, bassTop])
        {
            c.Glyph(Glyph.TimeSig3, x, t + 1 * S, S);
            c.Glyph(Glyph.TimeSig4, x, t + 3 * S, S);
        }
    }

    // ── One bar, both hands ───────────────────────────────────────────────
    /// <param name="fermata">Final bar: mark the closing chord as held (the performance holds it).</param>
    private void DrawBar(IScoreCanvas c, NoteEvent[] notes, float bx, float barW, float top, float bassTop, bool fermata)
    {
        float x0 = bx + BarPadLeft * S, inner = barW - BarPadTotal * S;
        float X(long tick) => x0 + tick * inner / PerformancePlan.TicksPerMeasure;

        foreach (var (channel, staffBottomY, bottomStep) in
                 ((byte, float, int)[])[(PerformancePlan.RightHand, top + 4 * S, TrebleBottom),
                                        (PerformancePlan.LeftHand,  bassTop + 4 * S, BassBottom)])
        {
            var staff  = new Staff(this, staffBottomY, bottomStep);
            var chords = BuildChords(notes, channel, X);
            var groups = BeamGroups(chords).ToList();
            foreach (var group in groups)
                staff.DrawGroup(c, group);

            if (fermata && groups.Count > 0)
                staff.DrawFermata(c, groups[^1], above: channel == PerformancePlan.RightHand);
        }
    }

    private static List<Chord> BuildChords(NoteEvent[] notes, byte channel, Func<long, float> x)
    {
        var onsets = notes.Where(n => n.Channel == channel)
                          .GroupBy(n => n.AbsTick)
                          .OrderBy(g => g.Key)
                          .ToList();

        var altered = new Dictionary<int, int>();       // step → alteration shown so far in this bar
        var chords  = new List<Chord>(onsets.Count);

        for (int i = 0; i < onsets.Count; i++)
        {
            long tick = onsets[i].Key;
            long next = i + 1 < onsets.Count ? onsets[i + 1].Key : PerformancePlan.TicksPerMeasure;
            var value = NoteValue.FromTicks(next - tick);

            var heads = onsets[i]
                .Select(n => Spell(n.Pitch))
                .DistinctBy(h => h.Step)
                .OrderBy(h => h.Step)
                .Select(h =>
                {
                    int current = altered.TryGetValue(h.Step, out var a) ? a : KeyAlteration(h.Step);
                    altered[h.Step] = h.Alteration;
                    return h with { ShowAccidental = h.Alteration != current };
                })
                .ToArray();

            chords.Add(new Chord(tick, x(tick), value, heads));
        }
        return chords;
    }

    /// <summary>Eighths and shorter are beamed together within a beat.</summary>
    private static IEnumerable<List<Chord>> BeamGroups(List<Chord> chords)
    {
        var group = new List<Chord>();
        foreach (var ch in chords)
        {
            bool beamable = ch.Value.Flags > 0;
            if (group.Count > 0 && (!beamable || Beat(group[0]) != Beat(ch)))
            {
                yield return group;
                group = [];
            }
            group.Add(ch);
            if (!beamable) { yield return group; group = []; }
        }
        if (group.Count > 0) yield return group;

        static long Beat(Chord c) => c.Tick / PerformancePlan.TicksPerQuarter;
    }

    // ── Pitch spelling in G major ─────────────────────────────────────────
    //                                     C  C♯ D  D♯ E  F  F♯ G  G♯ A  A♯ B
    private static readonly int[] Letter = [0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6];
    private static readonly int[] Alter  = [0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0];

    private static Head Spell(int midi)
    {
        int pc = midi % 12, octave = midi / 12 - 1;
        return new Head(octave * 7 + Letter[pc], Alter[pc], ShowAccidental: false);
    }

    private static int KeyAlteration(int step) => step % 7 == 3 ? 1 : 0;   // F is sharp

    // ── Types ─────────────────────────────────────────────────────────────
    private readonly record struct Head(int Step, int Alteration, bool ShowAccidental);

    private sealed record Chord(long Tick, float X, NoteValue Value, Head[] Heads)
    {
        public int Low  => Heads[0].Step;
        public int High => Heads[^1].Step;
    }

    private readonly record struct NoteValue(bool Hollow, bool Stem, int Flags, bool Dotted)
    {
        public static NoteValue FromTicks(long ticks)
        {
            // Snap to the nearest notatable value.
            (int t, NoteValue v)[] table =
            [
                (1440, new(true,  true, 0, true)),   // dotted half
                (960,  new(true,  true, 0, false)),  // half
                (720,  new(false, true, 0, true)),   // dotted quarter
                (480,  new(false, true, 0, false)),  // quarter
                (360,  new(false, true, 1, true)),   // dotted eighth
                (240,  new(false, true, 1, false)),  // eighth
                (180,  new(false, true, 2, true)),   // dotted sixteenth
                (120,  new(false, true, 2, false)),  // sixteenth
            ];
            return table.MinBy(e => Math.Abs(e.t - ticks)).v;
        }
    }

    /// <summary>One staff of one bar: maps steps to y and draws chord groups.</summary>
    private sealed class Staff(ScoreEngraver e, float bottomY, int bottomStep)
    {
        private float S => e.S;
        private int Middle => bottomStep + 4;
        private float Y(int step) => bottomY - (step - bottomStep) * S / 2;

        /// <summary>Fermata over (treble) or under (bass) the group's last chord, clear of stems and the staff.</summary>
        public void DrawFermata(IScoreCanvas c, List<Chord> group, bool above)
        {
            const float Gap = 0.7f, HalfWidth = 1.21f;       // fermata glyph is 2.42 spaces wide
            bool up = Middle - group.Min(g => g.Low) > group.Max(g => g.High) - Middle;
            var ch = group[^1];
            float x = ch.X + (HeadW / 2 - HalfWidth) * S;

            if (above)
            {
                float extent = up && ch.Value.Stem ? Math.Min(Y(ch.High) - StemLen * S, Y(Middle)) : Y(ch.High) - S / 2;
                c.Glyph(Glyph.FermataAbove, x, Math.Min(Y(bottomStep + 8), extent) - Gap * S, S);
            }
            else
            {
                float extent = !up && ch.Value.Stem ? Math.Max(Y(ch.Low) + StemLen * S, Y(Middle)) : Y(ch.Low) + S / 2;
                c.Glyph(Glyph.FermataBelow, x, Math.Max(Y(bottomStep), extent) + Gap * S, S);
            }
        }

        public void DrawGroup(IScoreCanvas c, List<Chord> group)
        {
            // Direction from the note farthest from the middle line, across the whole group.
            int low = group.Min(g => g.Low), high = group.Max(g => g.High);
            bool up = Middle - low > high - Middle;

            foreach (var ch in group) DrawHeads(c, ch, up);

            if (group.Count == 1)
            {
                var ch = group[0];
                if (!ch.Value.Stem) return;
                float sx  = StemX(ch, up);
                float tip = up ? Math.Min(Y(ch.High) - StemLen * S, Y(Middle))
                               : Math.Max(Y(ch.Low) + StemLen * S, Y(Middle));
                DrawStem(c, ch, up, sx, tip);
                if (ch.Value.Flags > 0)
                {
                    var flag = (ch.Value.Flags, up) switch
                    {
                        (1, true) => Glyph.Flag8thUp,   (1, false) => Glyph.Flag8thDown,
                        (_, true) => Glyph.Flag16thUp,  (_, false) => Glyph.Flag16thDown,
                    };
                    c.Glyph(flag, sx - StemW * S / 2, tip, S);
                }
                return;
            }

            // ── Beamed group ──
            var first = group[0]; var last = group[^1];
            float x1 = StemX(first, up), x2 = StemX(last, up);
            float Ideal(Chord ch) => up ? Y(ch.High) - StemLen * S : Y(ch.Low) + StemLen * S;
            float slope = Math.Clamp((Ideal(last) - Ideal(first)) / Math.Max(1, x2 - x1), -0.12f, 0.12f);
            float y1 = Ideal(first);

            // Keep every stem at least MinStem long, and the beam clear of the middle line side.
            foreach (var ch in group)
            {
                float at = y1 + slope * (StemX(ch, up) - x1);
                y1 += up ? Math.Min(0, Y(ch.High) - MinStem * S - at)
                         : Math.Max(0, Y(ch.Low) + MinStem * S - at);
            }
            float BeamY(float x) => y1 + slope * (x - x1);

            foreach (var ch in group) DrawStem(c, ch, up, StemX(ch, up), BeamY(StemX(ch, up)));

            float half = StemW * S / 2, thick = BeamW * S;
            float Edge(float y) => up ? y : y - thick;                    // beam's top edge
            c.Beam(x1 - half, Edge(BeamY(x1)), x2 + half, Edge(BeamY(x2)), thick);

            // Secondary beams for sixteenths (full between neighbours, a stub when alone).
            float off = (up ? 1 : -1) * BeamStep * S;
            for (int i = 0; i < group.Count; i++)
            {
                if (group[i].Value.Flags < 2) continue;
                float xa = StemX(group[i], up);
                if (i + 1 < group.Count && group[i + 1].Value.Flags >= 2)
                {
                    float xb = StemX(group[i + 1], up);
                    c.Beam(xa - half, Edge(BeamY(xa)) + off, xb + half, Edge(BeamY(xb)) + off, thick);
                }
                else if (i == 0 || group[i - 1].Value.Flags < 2)
                {
                    float stub = (i == group.Count - 1 ? -1.1f : 1.1f) * S;
                    float xs = xa + stub;
                    c.Beam(Math.Min(xa, xs) - half, Edge(BeamY(Math.Min(xa, xs))) + off,
                           Math.Max(xa, xs) + half, Edge(BeamY(Math.Max(xa, xs))) + off, thick);
                }
            }
        }

        private float StemX(Chord ch, bool up) => up ? ch.X + (HeadW - StemW / 2) * S : ch.X + StemW / 2 * S;

        private void DrawStem(IScoreCanvas c, Chord ch, bool up, float x, float tip)
        {
            float from = up ? Y(ch.Low) - HeadStemDy * S : Y(ch.High) + HeadStemDy * S;
            c.Line(x, from, x, tip, StemW * S);
        }

        private void DrawHeads(IScoreCanvas c, Chord ch, bool up)
        {
            // Seconds: alternate heads go to the other side of the stem.
            var heads = up ? ch.Heads : ch.Heads.Reverse().ToArray();
            int prev = int.MinValue; bool prevShifted = false;

            foreach (var h in heads)
            {
                bool shifted = Math.Abs(h.Step - prev) == 1 && !prevShifted;
                float x = ch.X + (shifted ? (up ? 1 : -1) * (HeadW - StemW) * S : 0);
                float y = Y(h.Step);

                // Ledger lines
                int top = bottomStep + 8;
                for (int l = top + 2; l <= h.Step; l += 2)       DrawLedger(c, x, Y(l));
                for (int l = bottomStep - 2; l >= h.Step; l -= 2) DrawLedger(c, x, Y(l));

                c.Glyph(ch.Value.Hollow ? Glyph.NoteheadHalf : Glyph.NoteheadBlack, x, y, S);

                if (h.ShowAccidental)
                    c.Glyph(h.Alteration > 0 ? Glyph.Sharp : Glyph.Natural, ch.X - 1.4f * S, y, S);

                if (ch.Value.Dotted)
                {
                    bool onLine = (h.Step - bottomStep) % 2 == 0;
                    c.Glyph(Glyph.AugmentationDot, ch.X + (HeadW + 0.35f) * S, onLine ? y - S / 2 : y, S);
                }

                prev = h.Step; prevShifted = shifted;
            }
        }

        private void DrawLedger(IScoreCanvas c, float x, float y)
            => c.Line(x - LedgerExt * S, y, x + (HeadW + LedgerExt) * S, y, LedgerW * S);
    }
}
