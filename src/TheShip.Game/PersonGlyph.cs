namespace TheShip.Game;

/// <summary>
/// The awake-person marker: a round head over rounded shoulders, symmetric about the centre. Pure maths (no MonoGame) so it is
/// tested; <c>Painter.Marker</c> turns the coverage into a texture.
/// </summary>
internal static class PersonGlyph
{
    /// <summary>The glyph's size in layout units, and how many texels per unit the coverage has.</summary>
    public const int Size = 14;
    public const int Scale = 4;

    /// <summary>
    /// Coverage 0..1 of the glyph, <see cref="Size"/> x <see cref="Scale"/> texels square, row by row from the top, each texel the
    /// average of 4 x 4 sub-samples (the soft edge). Used by <c>Painter.Marker</c>, which draws it as a premultiplied white mask.
    /// </summary>
    public static float[] Coverage()
    {
        const int size = Size * Scale;
        const int sub = 4;
        var data = new float[size * size];
        const float mid = Size / 2f;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var hit = 0;
            for (var sy = 0; sy < sub; sy++)
            for (var sx = 0; sx < sub; sx++)
            {
                var px = (x + (sx + 0.5f) / sub) / Scale - mid;
                var py = (y + (sy + 0.5f) / sub) / Scale;
                // Head: circle r 3.2 centred at y 3.4. Shoulders: upper half of an ellipse 12 wide, 6.2 tall, base at y 14.
                var head = px * px + (py - 3.4f) * (py - 3.4f) <= 3.2f * 3.2f;
                var ey = (py - 14f) / 6.2f;
                var body = py <= 14f && (px / 6f) * (px / 6f) + ey * ey <= 1f;
                if (head || body) hit++;
            }
            data[y * size + x] = hit / (float)(sub * sub);
        }
        return data;
    }
}
