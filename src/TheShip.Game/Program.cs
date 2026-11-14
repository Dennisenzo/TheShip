namespace TheShip.Game;

/// <param name="Llm">A path, or a file name in Content/Models/llm; null for the default model.</param>
/// <param name="GpuLayers">Layers to offload to the GPU; 0 = CPU only.</param>
/// <param name="TourDir">Dev tool (--tour): play a fixed script, save screenshots here, then exit.</param>
/// <param name="TourWidth">Dev tool (--tour-size WxH): the pixel size of the tour's screenshots, as a window of that size would show the game.</param>
public sealed record Options(int Seed, string? Llm, int GpuLayers, string? TourDir = null, int TourWidth = Painter.ScreenWidth, int TourHeight = Painter.ScreenHeight);

internal static class Program
{
    // Usage: TheShip [--seed <n>] [--llm <file.gguf>] [--gpu-layers <n>] [--tour <dir>] [--tour-size <w>x<h>] [--render-music <dir>]
    // The game needs a GGUF model: without one it shows what is missing and does not start a voyage.
    private static void Main(string[] args)
    {
        if (Argument(args, "--render-music") is { } musicDir)
        {
            MusicRender.Run(musicDir); // dev tool: WAV files of every mood and the alarm, no window
            return;
        }
        var (tourWidth, tourHeight) = ParseSize(Argument(args, "--tour-size"));
        var options = new Options(
            Seed: int.TryParse(Argument(args, "--seed"), out var seed) ? seed : Environment.TickCount,
            Llm: Argument(args, "--llm"),
            GpuLayers: int.TryParse(Argument(args, "--gpu-layers"), out var layers) ? layers : 99,
            TourDir: Argument(args, "--tour"),
            TourWidth: tourWidth,
            TourHeight: tourHeight);
        using var game = new ShipGame(options);
        game.Run();
    }

    /// <summary>"1920x1080" to (1920, 1080); anything else (or too small) is the layout size.</summary>
    internal static (int Width, int Height) ParseSize(string? text)
    {
        var parts = text?.Split('x', 'X');
        return parts is { Length: 2 } && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) && w >= 320 && h >= 180 && w <= 8192 && h <= 8192
            ? (w, h)
            : (Painter.ScreenWidth, Painter.ScreenHeight);
    }

    private static string? Argument(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
