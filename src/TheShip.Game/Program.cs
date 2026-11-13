namespace TheShip.Game;

/// <param name="Llm">A path, or a file name in Content/Models/llm; null for the default model.</param>
/// <param name="GpuLayers">Layers to offload to the GPU; 0 = CPU only.</param>
/// <param name="TourDir">Dev tool (--tour): play a fixed script, save screenshots here, then exit.</param>
public sealed record Options(int Seed, string? Llm, int GpuLayers, string? TourDir = null);

internal static class Program
{
    // Usage: TheShip [--seed <n>] [--llm <file.gguf>] [--gpu-layers <n>] [--tour <dir>]
    // The game needs a GGUF model: without one it shows what is missing and does not start a voyage.
    private static void Main(string[] args)
    {
        var options = new Options(
            Seed: int.TryParse(Argument(args, "--seed"), out var seed) ? seed : Environment.TickCount,
            Llm: Argument(args, "--llm"),
            GpuLayers: int.TryParse(Argument(args, "--gpu-layers"), out var layers) ? layers : 99,
            TourDir: Argument(args, "--tour"));
        using var game = new ShipGame(options);
        game.Run();
    }

    private static string? Argument(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
