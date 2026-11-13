using System.Text;

namespace TheShip.Game;

/// <summary>
/// The pure text rules of the drawing kit (no MonoGame, so they are unit-tested): which characters the bundled font can show, and
/// word wrap for the monospaced font. <c>Painter.Printable</c> and <c>Painter.Wrap</c> call these.
/// </summary>
internal static class TextLayout
{
    /// <summary>
    /// The model may write characters the bundled font lacks: map the common ones, replace the rest with "?". A line break is kept
    /// (<see cref="Wrap"/> starts a new line there); a carriage return is dropped.
    /// </summary>
    public static string Printable(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                '‘' or '’' => "'",
                '“' or '”' => "\"",
                '–' or '—' => "-",
                '…' => "...",
                '\t' => " ",
                '\n' => "\n",
                '\r' => "",
                _ => ch is >= ' ' and <= 'ÿ' ? ch.ToString() : "?",
            });
        }
        return sb.ToString();
    }

    /// <summary>Word wrap by character count; a line break in the text starts a new line (an empty one for "\n\n").</summary>
    public static List<string> Wrap(string text, int columns)
    {
        var lines = new List<string>();
        columns = Math.Max(8, columns);
        foreach (var paragraph in Printable(text).Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' '))
            {
                var piece = word;
                while (piece.Length > columns)
                {
                    if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    lines.Add(piece[..columns]);
                    piece = piece[columns..];
                }
                if (line.Length > 0 && line.Length + 1 + piece.Length > columns)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                }
                if (line.Length > 0)
                    line.Append(' ');
                line.Append(piece);
            }
            lines.Add(line.ToString());
        }
        // A non-breaking space keeps a number with its unit on one line; it is drawn as an ordinary space.
        for (var i = 0; i < lines.Count; i++)
            lines[i] = lines[i].Replace(' ', ' ');
        return lines;
    }
}
