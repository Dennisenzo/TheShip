using System.Runtime.InteropServices;

namespace TheShip.Game;

/// <summary>Window calls MonoGame doesn't expose. DesktopGL runs on SDL2, and <c>GameWindow.Handle</c> is the SDL window.</summary>
internal static class WindowState
{
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_MaximizeWindow(IntPtr window);

    /// <summary>Maximizes the window. The resize reaches the game like a drag of the window edge, so the view rescales.</summary>
    public static void Maximize(IntPtr window)
    {
        try
        {
            SDL_MaximizeWindow(window);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            // Not on SDL: the window keeps its starting size.
        }
    }
}
