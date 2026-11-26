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

    private float _width;

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

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (Math.Abs(width - _width) > 0.5) { _width = (float)width; Relayout(); }
    }

    private ScoreEngraver Engraver => new(ScoreEngraver.StaffSpaceFor(_width));

    private void Relayout()
    {
        if (Measures is not { Count: > 0 } m || _width <= 0) return;
        HeightRequest = Engraver.Height(m.Count, _width);
        Invalidate();
    }

    void IDrawable.Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Measures is not { Count: > 0 } m || _width <= 0) return;
        Engraver.Draw(new MauiScoreCanvas(canvas, Ink), m, _width);
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
