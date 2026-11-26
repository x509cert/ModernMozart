using ModernMozart.Core.Notation;

namespace ModernMozart.App.Controls;

/// <summary>Grand-staff score of a list of measures, engraved by <see cref="ScoreEngraver"/>.</summary>
public sealed class ScoreView : GraphicsView, IDrawable
{
    public static readonly BindableProperty MeasuresProperty = BindableProperty.Create(
        nameof(Measures), typeof(IReadOnlyList<int>), typeof(ScoreView),
        propertyChanged: (b, _, _) => ((ScoreView)b).Relayout());

    public static readonly BindableProperty InkProperty = BindableProperty.Create(
        nameof(Ink), typeof(Color), typeof(ScoreView), Colors.Black,
        propertyChanged: (b, _, _) => ((ScoreView)b).Invalidate());

    /// <summary>
    /// Playback position as bar index + fraction through the bar (e.g. 3.5 = halfway through the
    /// fourth bar). Negative = not playing.
    /// </summary>
    public static readonly BindableProperty PlayheadProperty = BindableProperty.Create(
        nameof(Playhead), typeof(double), typeof(ScoreView), -1.0,
        propertyChanged: (b, _, _) => ((ScoreView)b).Invalidate());

    public static readonly BindableProperty HighlightProperty = BindableProperty.Create(
        nameof(Highlight), typeof(Color), typeof(ScoreView), Colors.Gold,
        propertyChanged: (b, _, _) => ((ScoreView)b).Invalidate());

    /// <summary>Fixed bars per line (0 = as many as fit); the staff is sized to fit them.</summary>
    public static readonly BindableProperty BarsPerRowProperty = BindableProperty.Create(
        nameof(BarsPerRow), typeof(int), typeof(ScoreView), 0, propertyChanged: (b, _, _) => ((ScoreView)b).Relayout());

    /// <summary>Index of the first measure within the whole piece (time signature, barlines).</summary>
    public static readonly BindableProperty FirstBarIndexProperty = BindableProperty.Create(
        nameof(FirstBarIndex), typeof(int), typeof(ScoreView), 0, propertyChanged: (b, _, _) => ((ScoreView)b).Relayout());

    public static readonly BindableProperty PieceLengthProperty = BindableProperty.Create(
        nameof(PieceLength), typeof(int), typeof(ScoreView), 0, propertyChanged: (b, _, _) => ((ScoreView)b).Relayout());

    private float _width;

    public int BarsPerRow    { get => (int)GetValue(BarsPerRowProperty);    set => SetValue(BarsPerRowProperty, value); }
    public int FirstBarIndex { get => (int)GetValue(FirstBarIndexProperty); set => SetValue(FirstBarIndexProperty, value); }
    public int PieceLength   { get => (int)GetValue(PieceLengthProperty);   set => SetValue(PieceLengthProperty, value); }

    public ScoreView() => Drawable = this;

    public IReadOnlyList<int>? Measures
    {
        get => (IReadOnlyList<int>?)GetValue(MeasuresProperty);
        set => SetValue(MeasuresProperty, value);
    }

    public Color Ink
    {
        get => (Color)GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    public double Playhead
    {
        get => (double)GetValue(PlayheadProperty);
        set => SetValue(PlayheadProperty, value);
    }

    public Color Highlight
    {
        get => (Color)GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    /// <summary>Top of the line of music containing <paramref name="bar"/>, relative to this view.</summary>
    public double SystemTop(int bar) => _width > 0 ? Engraver.Bar(bar, _width).SystemTop : 0;

    /// <summary>Height of one line of music (grand staff with margins).</summary>
    public double SystemHeight => _width > 0 ? ScoreEngraver.SystemHeight * Engraver.S : 0;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (Math.Abs(width - _width) > 0.5) { _width = (float)width; Relayout(); }
    }

    private ScoreEngraver Engraver => new(BarsPerRow > 0 ? ScoreEngraver.StaffSpaceToFit(_width, BarsPerRow)
                                                        : ScoreEngraver.StaffSpaceFor(_width))
    {
        FixedBarsPerRow = BarsPerRow,
        FirstBarIndex   = FirstBarIndex,
        PieceLength     = PieceLength,
    };

    private void Relayout()
    {
        if (Measures is not { Count: > 0 } m || _width <= 0) return;
        HeightRequest = Engraver.Height(m.Count, _width);
        Invalidate();
    }

    void IDrawable.Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Measures is not { Count: > 0 } m || _width <= 0) return;
        var engraver = Engraver;

        // Current bar wash + playhead, under the notes.
        if (Playhead >= 0 && (int)Playhead < m.Count)
        {
            var bar = engraver.Bar((int)Playhead, _width);
            float pad = engraver.S;
            canvas.FillColor = Highlight.WithAlpha(0.16f);
            canvas.FillRoundedRectangle(bar.X, bar.StaffTop - pad, bar.Width, bar.StaffBottom - bar.StaffTop + 2 * pad, pad * 0.6f);
            canvas.StrokeColor = Highlight;
            canvas.StrokeSize  = Math.Max(1.5f, pad * 0.3f);
            float x = bar.At(Playhead - (int)Playhead);
            canvas.DrawLine(x, bar.StaffTop - pad, x, bar.StaffBottom + pad);
        }

        engraver.Draw(new MauiScoreCanvas(canvas, Ink), m, _width);
    }

    /// <summary>Adapts the engraver's primitives to Microsoft.Maui.Graphics.</summary>
    private sealed class MauiScoreCanvas : IScoreCanvas
    {
        private static readonly Dictionary<Glyph, PathF> Paths = [];
        private readonly ICanvas _c;

        public MauiScoreCanvas(ICanvas canvas, Color ink)
        {
            _c = canvas;
            _c.StrokeColor = ink;
            _c.FillColor   = ink;
            _c.FontColor   = ink;
            _c.StrokeLineCap = LineCap.Butt;
            _c.Antialias   = true;
        }

        public void Line(float x1, float y1, float x2, float y2, float thickness)
        {
            _c.StrokeSize = thickness;
            _c.DrawLine(x1, y1, x2, y2);
        }

        public void Glyph(Glyph glyph, float x, float y, float scale)
        {
            if (!Paths.TryGetValue(glyph, out var path))
                Paths[glyph] = path = PathBuilder.Build(SmuflGlyphs.PathData(glyph));

            _c.SaveState();
            _c.Translate(x, y);
            _c.Scale(scale, scale);
            _c.FillPath(path);
            _c.RestoreState();
        }

        public void Beam(float x1, float y1, float x2, float y2, float thickness)
        {
            var p = new PathF();
            p.MoveTo(x1, y1);
            p.LineTo(x2, y2);
            p.LineTo(x2, y2 + thickness);
            p.LineTo(x1, y1 + thickness);
            p.Close();
            _c.FillPath(p);
        }

        public void Text(string text, float x, float y, float size)
        {
            _c.Font     = new Microsoft.Maui.Graphics.Font("serif", FontWeights.Normal, FontStyleType.Italic);
            _c.FontSize = size;
            _c.DrawString(text, x, y, HorizontalAlignment.Left);
        }
    }
}
