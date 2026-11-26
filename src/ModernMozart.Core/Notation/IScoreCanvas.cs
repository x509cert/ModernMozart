namespace ModernMozart.Core.Notation;

/// <summary>
/// The few drawing primitives the engraver needs. All coordinates are in device units
/// (pixels/dp), y pointing down. Implementations choose the ink colour.
/// </summary>
public interface IScoreCanvas
{
    /// <summary>A straight line of the given thickness (butt caps).</summary>
    void Line(float x1, float y1, float x2, float y2, float thickness);

    /// <summary>A SMuFL glyph with its origin at (x, y); <paramref name="scale"/> = device units per staff space.</summary>
    void Glyph(Glyph glyph, float x, float y, float scale);

    /// <summary>A beam: parallelogram from (x1, y1) to (x2, y2) along its top edge.</summary>
    void Beam(float x1, float y1, float x2, float y2, float thickness);

    /// <summary>Small label text, left-aligned, with (x, y) at the text's baseline.</summary>
    void Text(string text, float x, float y, float size);
}
