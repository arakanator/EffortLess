using System.Runtime.InteropServices;

namespace EffortLess.App;

internal static class PlayerInput
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int key);

    internal static bool IsKeyDown(int key) => (GetKeyState(key) & 0x8000) != 0;
}
