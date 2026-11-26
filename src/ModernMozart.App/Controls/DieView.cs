namespace ModernMozart.App.Controls;

/// <summary>A single die face (1–6) drawn with pips.</summary>
public sealed class DieView : GraphicsView, IDrawable
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(int), typeof(DieView), 1, propertyChanged: Redraw);

    public static readonly BindableProperty FaceColorProperty = BindableProperty.Create(
        nameof(FaceColor), typeof(Color), typeof(DieView), Colors.White, propertyChanged: Redraw);

    public static readonly BindableProperty PipColorProperty = BindableProperty.Create(
        nameof(PipColor), typeof(Color), typeof(DieView), Colors.Black, propertyChanged: Redraw);

    public DieView()
    {
        Drawable = this;
        WidthRequest = HeightRequest = 18;
        BackgroundColor = Colors.Transparent;
    }

    public int   Value     { get => (int)GetValue(ValueProperty);       set => SetValue(ValueProperty, value); }
    public Color FaceColor { get => (Color)GetValue(FaceColorProperty); set => SetValue(FaceColorProperty, value); }
    public Color PipColor  { get => (Color)GetValue(PipColorProperty);  set => SetValue(PipColorProperty, value); }

    private static void Redraw(BindableObject b, object o, object n) => ((DieView)b).Invalidate();

    // Pip positions on a 3×3 grid (0 = left/top, 2 = right/bottom).
    private static readonly (int x, int y)[][] Pips =
    [
        [],
        [(1, 1)],
        [(0, 0), (2, 2)],
        [(0, 0), (1, 1), (2, 2)],
        [(0, 0), (2, 0), (0, 2), (2, 2)],
        [(0, 0), (2, 0), (1, 1), (0, 2), (2, 2)],
        [(0, 0), (2, 0), (0, 1), (2, 1), (0, 2), (2, 2)],
    ];

    void IDrawable.Draw(ICanvas canvas, RectF r)
    {
        float size = Math.Min(r.Width, r.Height) - 1;
        float x0 = r.Center.X - size / 2, y0 = r.Center.Y - size / 2;

        canvas.Antialias = true;
        canvas.FillColor = FaceColor;
        canvas.FillRoundedRectangle(x0, y0, size, size, size * 0.22f);

        canvas.FillColor = PipColor;
        float step = size * 0.27f, radius = size * 0.095f;
        foreach (var (px, py) in Pips[Math.Clamp(Value, 1, 6)])
            canvas.FillCircle(x0 + size / 2 + (px - 1) * step, y0 + size / 2 + (py - 1) * step, radius);
    }
}
